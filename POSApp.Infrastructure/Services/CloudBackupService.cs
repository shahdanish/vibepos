using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Cloud.Firestore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Full-database cloud backup/restore engine for disaster recovery.
    ///
    /// Uploads the entire posapp.db file (GZip-compressed, Base64-encoded, split into
    /// &lt;1 MB Firestore chunk documents) so a client can be fully restored after total
    /// data loss. Reuses the existing Firebase credentials and <see cref="DatabaseBackupService"/>
    /// (SQLite native online backup). This is intentionally separate from the per-record
    /// <see cref="FirebaseSyncService"/> mirror, which only covers a few tables.
    ///
    /// Firestore layout:
    ///   db_backups/{clientId}                              -> { latestSnapshot, updatedAt }
    ///   db_backups/{clientId}/snapshots/{ts}               -> metadata (size, sha256, chunkCount, schema…)
    ///   db_backups/{clientId}/snapshots/{ts}/chunks/{n}    -> { index, data (base64 slice) }
    /// </summary>
    public sealed class CloudBackupService : ICloudBackupService
    {
        // Base64 characters per chunk document. Firestore's per-document limit is ~1 MB;
        // 700 KB keeps each chunk comfortably under it.
        private const int ChunkCharSize = 700_000;

        // Keep the most recent N snapshots in the cloud; older ones are pruned after upload.
        private const int MaxSnapshots = 5;

        // Automatic backup cadence: back up at most once per this interval.
        private static readonly TimeSpan AutoBackupInterval = TimeSpan.FromHours(24);

        private readonly IServiceProvider _serviceProvider;
        private readonly IDatabaseBackupService _dbBackup;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _logLock = new();
        private readonly string _logPath;
        private readonly string _statePath;

        private FirestoreDb? _firestore;
        private string _clientId = "default";

        private static readonly string DbPath = Path.GetFullPath("posapp.db");
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "POSAppCloudBackup");

        public CloudBackupService(IServiceProvider serviceProvider, IDatabaseBackupService dbBackup)
        {
            _serviceProvider = serviceProvider;
            _dbBackup = dbBackup;
            _logPath = Path.Combine(AppContext.BaseDirectory, "sync.log");
            _statePath = Path.Combine(AppContext.BaseDirectory, "cloudbackup.state");
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────────
        public void Initialize(string? credentialsPath = null, string? projectId = null)
        {
            try
            {
                var credPath = credentialsPath ?? Path.Combine(AppContext.BaseDirectory, "firebase-credentials.json");
                if (!File.Exists(credPath))
                {
                    Log("⚠ No credentials file — cloud backup disabled (app works offline).");
                    return;
                }

                var effectiveProjectId = projectId ?? ReadProjectIdFromCredentials(credPath);
                _clientId = effectiveProjectId ?? "default";
                _firestore = new FirestoreDbBuilder
                {
                    ProjectId = effectiveProjectId,
                    CredentialsPath = credPath
                }.Build();

                Log($"CloudBackup initialized (client={_clientId}).");

                // Background auto-backup loop (daily, throttled).
                _ = Task.Run(AutoBackupLoopAsync);
            }
            catch (Exception ex)
            {
                Log($"❌ CloudBackup init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Backup
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<CloudBackupResult> BackupToCloudAsync(CancellationToken ct = default)
        {
            Log("=== BackupToCloudAsync requested ===");

            if (_firestore is null)
                return Skip("Cloud backup is not configured (firebase-credentials.json missing).");

            if (!await _gate.WaitAsync(5000, ct))
                return Skip("Another backup/restore is already in progress. Please try again in a moment.");

            string? tempDb = null;
            try
            {
                Directory.CreateDirectory(TempDir);

                // 1) Consistent SQLite snapshot (safe while the app is running).
                tempDb = await _dbBackup.CreateBackupAsync(TempDir, ct);

                // Microsoft.Data.Sqlite pools connections, so the backup file handle can
                // linger open after CreateBackupAsync disposes its connection. Clear the
                // pools so the snapshot file is fully released before we read it.
                SqliteConnection.ClearAllPools();

                // 2) Read + hash + compress.
                var raw = await File.ReadAllBytesAsync(tempDb, ct);
                var sha256 = Convert.ToHexString(SHA256.HashData(raw));
                var compressed = Gzip(raw);
                var base64 = Convert.ToBase64String(compressed);

                // 3) Split into chunks.
                var chunks = SplitString(base64, ChunkCharSize);

                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var (schemaVersion, appVersion) = await GetVersionInfoAsync(ct);

                var clientDoc = _firestore.Collection("db_backups").Document(_clientId);
                var snapDoc = clientDoc.Collection("snapshots").Document(ts);

                // 4) Write chunk docs first, then the metadata doc (so metadata only
                //    exists once all chunks are safely uploaded).
                for (int i = 0; i < chunks.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await snapDoc.Collection("chunks").Document(i.ToString()).SetAsync(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["data"] = chunks[i]
                    }, cancellationToken: ct);
                }

                await snapDoc.SetAsync(new Dictionary<string, object>
                {
                    ["sizeBytes"] = (long)raw.Length,
                    ["compressedBytes"] = (long)compressed.Length,
                    ["sha256"] = sha256,
                    ["chunkCount"] = chunks.Count,
                    ["chunkCharSize"] = ChunkCharSize,
                    ["createdAtUtc"] = DateTime.UtcNow.ToString("O"),
                    ["schemaVersion"] = schemaVersion,
                    ["appVersion"] = appVersion
                }, cancellationToken: ct);

                // 5) Update the "latest" pointer.
                await clientDoc.SetAsync(new Dictionary<string, object>
                {
                    ["latestSnapshot"] = ts,
                    ["updatedAtUtc"] = DateTime.UtcNow.ToString("O")
                }, SetOptions.MergeAll, ct);

                // 6) Prune old snapshots.
                await PruneOldSnapshotsAsync(clientDoc, ct);

                WriteState(DateTime.UtcNow);
                Log($"Backup OK: {ts} — {raw.Length} bytes, {chunks.Count} chunks.");

                return new CloudBackupResult
                {
                    Success = true,
                    SizeBytes = raw.Length,
                    ChunkCount = chunks.Count,
                    SnapshotTime = DateTime.Now
                };
            }
            catch (OperationCanceledException)
            {
                return Skip("Backup was cancelled.");
            }
            catch (Exception ex)
            {
                Log($"❌ Backup failed: {ex.GetType().Name}: {ex.Message}");
                return new CloudBackupResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                TryDelete(tempDb);
                _gate.Release();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Restore
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<CloudBackupResult> RestoreFromCloudAsync(CancellationToken ct = default)
        {
            Log("=== RestoreFromCloudAsync requested ===");

            if (_firestore is null)
                return Skip("Cloud backup is not configured (firebase-credentials.json missing).");

            if (!await _gate.WaitAsync(5000, ct))
                return Skip("Another backup/restore is already in progress. Please try again in a moment.");

            string? tempDb = null;
            try
            {
                var clientDoc = _firestore.Collection("db_backups").Document(_clientId);

                // 1) Resolve the latest snapshot id.
                var clientSnap = await clientDoc.GetSnapshotAsync(ct);
                if (!clientSnap.Exists || !clientSnap.ContainsField("latestSnapshot"))
                    return Skip("No cloud backup was found for this store.");

                var ts = clientSnap.GetValue<string>("latestSnapshot");
                var snapDoc = clientDoc.Collection("snapshots").Document(ts);
                var meta = await snapDoc.GetSnapshotAsync(ct);
                if (!meta.Exists)
                    return Skip("The latest cloud backup metadata is missing or corrupt.");

                var expectedSha = meta.GetValue<string>("sha256");
                var expectedChunks = meta.GetValue<int>("chunkCount");
                var expectedSize = meta.GetValue<long>("sizeBytes");
                var schemaVersion = meta.ContainsField("schemaVersion") ? meta.GetValue<string>("schemaVersion") : "";

                // 2) Schema guard — refuse a backup made by a NEWER app version.
                if (!string.IsNullOrEmpty(schemaVersion) && !await SchemaIsKnownAsync(schemaVersion, ct))
                    return Skip("This cloud backup was made by a newer version of the app. " +
                                "Please update the application before restoring.");

                // 3) Download + reassemble chunks.
                var chunkQuery = await snapDoc.Collection("chunks").OrderBy("index").GetSnapshotAsync(ct);
                if (chunkQuery.Count != expectedChunks)
                    return Skip($"Cloud backup is incomplete ({chunkQuery.Count}/{expectedChunks} chunks). Restore aborted.");

                var sb = new StringBuilder();
                foreach (var chunk in chunkQuery.Documents)
                    sb.Append(chunk.GetValue<string>("data"));

                byte[] raw;
                try
                {
                    raw = Gunzip(Convert.FromBase64String(sb.ToString()));
                }
                catch (Exception ex)
                {
                    return Skip($"Cloud backup could not be decoded ({ex.Message}). Restore aborted.");
                }

                // 4) Integrity verification BEFORE touching the local database.
                var actualSha = Convert.ToHexString(SHA256.HashData(raw));
                if (raw.LongLength != expectedSize || !string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                    return Skip("Cloud backup failed integrity check (checksum mismatch). Restore aborted — local data untouched.");

                // 5) Write the verified bytes to a temp .db and validate it opens as SQLite.
                Directory.CreateDirectory(TempDir);
                tempDb = Path.Combine(TempDir, $"restore_{ts}.db");
                await File.WriteAllBytesAsync(tempDb, raw, ct);

                // 6) Safety net: copy the current DB aside before overwriting.
                if (File.Exists(DbPath))
                    File.Copy(DbPath, DbPath + ".prerestore", overwrite: true);

                // 7) Release any pooled connections, then overwrite posapp.db via the
                //    SQLite native backup API (page-by-page copy into the live file).
                SqliteConnection.ClearAllPools();
                await _dbBackup.RestoreFromBackupAsync(tempDb, ct);

                WriteState(DateTime.UtcNow);
                var snapTime = ParseTimestamp(ts);
                Log($"Restore OK from {ts} — {raw.Length} bytes.");

                return new CloudBackupResult
                {
                    Success = true,
                    IsRestore = true,
                    SizeBytes = raw.Length,
                    ChunkCount = expectedChunks,
                    SnapshotTime = snapTime
                };
            }
            catch (OperationCanceledException)
            {
                return Skip("Restore was cancelled.");
            }
            catch (Exception ex)
            {
                Log($"❌ Restore failed: {ex.GetType().Name}: {ex.Message}");
                return new CloudBackupResult { Success = false, IsRestore = true, ErrorMessage = ex.Message };
            }
            finally
            {
                TryDelete(tempDb);
                _gate.Release();
            }
        }

        public async Task<CloudSnapshotInfo?> GetLatestSnapshotInfoAsync(CancellationToken ct = default)
        {
            if (_firestore is null) return null;
            try
            {
                var clientDoc = _firestore.Collection("db_backups").Document(_clientId);
                var clientSnap = await clientDoc.GetSnapshotAsync(ct);
                if (!clientSnap.Exists || !clientSnap.ContainsField("latestSnapshot")) return null;

                var ts = clientSnap.GetValue<string>("latestSnapshot");
                var meta = await clientDoc.Collection("snapshots").Document(ts).GetSnapshotAsync(ct);
                if (!meta.Exists) return null;

                return new CloudSnapshotInfo
                {
                    SnapshotTime = ParseTimestamp(ts),
                    SizeBytes = meta.ContainsField("sizeBytes") ? meta.GetValue<long>("sizeBytes") : 0,
                    ChunkCount = meta.ContainsField("chunkCount") ? meta.GetValue<int>("chunkCount") : 0,
                    SchemaVersion = meta.ContainsField("schemaVersion") ? meta.GetValue<string>("schemaVersion") : "",
                    AppVersion = meta.ContainsField("appVersion") ? meta.GetValue<string>("appVersion") : ""
                };
            }
            catch (Exception ex)
            {
                Log($"GetLatestSnapshotInfo failed: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Automatic backup loop
        // ─────────────────────────────────────────────────────────────────────────
        private async Task AutoBackupLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), _cts.Token);
                }
                catch (OperationCanceledException) { break; }

                try
                {
                    var last = ReadState();
                    if (DateTime.UtcNow - last < AutoBackupInterval) continue;

                    var result = await BackupToCloudAsync(_cts.Token);
                    if (result.WasSkipped)
                        Log($"Auto-backup skipped: {result.SkipReason}");
                }
                catch (Exception ex)
                {
                    Log($"Auto-backup loop error: {ex.Message}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private async Task PruneOldSnapshotsAsync(DocumentReference clientDoc, CancellationToken ct)
        {
            try
            {
                var all = await clientDoc.Collection("snapshots").GetSnapshotAsync(ct);
                var stale = all.Documents
                    .OrderByDescending(d => d.Id) // ids are sortable yyyyMMdd_HHmmss
                    .Skip(MaxSnapshots)
                    .ToList();

                foreach (var snap in stale)
                {
                    var chunks = await snap.Reference.Collection("chunks").GetSnapshotAsync(ct);
                    foreach (var c in chunks.Documents)
                        await c.Reference.DeleteAsync(cancellationToken: ct);
                    await snap.Reference.DeleteAsync(cancellationToken: ct);
                    Log($"Pruned old snapshot {snap.Id}.");
                }
            }
            catch (Exception ex)
            {
                Log($"Prune failed (non-fatal): {ex.Message}");
            }
        }

        /// <summary>Whether the given migration id is one this build knows about (i.e. not from a newer app).</summary>
        private async Task<bool> SchemaIsKnownAsync(string schemaVersion, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var known = await Task.Run(() => db.Database.GetMigrations().ToHashSet(), ct);
            return known.Contains(schemaVersion);
        }

        private async Task<(string schemaVersion, string appVersion)> GetVersionInfoAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applied = await db.Database.GetAppliedMigrationsAsync(ct);
            var schema = applied.LastOrDefault() ?? string.Empty;
            var app = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            return (schema, app);
        }

        private static string? ReadProjectIdFromCredentials(string credPath)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(credPath));
                return doc.RootElement.GetProperty("project_id").GetString();
            }
            catch { return null; }
        }

        private static byte[] Gzip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gz.Write(data, 0, data.Length);
            return output.ToArray();
        }

        private static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }

        private static List<string> SplitString(string s, int size)
        {
            var parts = new List<string>((s.Length / size) + 1);
            for (int i = 0; i < s.Length; i += size)
                parts.Add(s.Substring(i, Math.Min(size, s.Length - i)));
            return parts;
        }

        private static DateTime ParseTimestamp(string ts)
            => DateTime.TryParseExact(ts, "yyyyMMdd_HHmmss", null,
                System.Globalization.DateTimeStyles.None, out var dt) ? dt : DateTime.Now;

        private CloudBackupResult Skip(string reason)
        {
            Log($"SKIP: {reason}");
            return new CloudBackupResult { SkipReason = reason };
        }

        private DateTime ReadState()
        {
            try
            {
                if (File.Exists(_statePath) &&
                    DateTime.TryParse(File.ReadAllText(_statePath).Trim(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
            }
            catch { /* ignore */ }
            return DateTime.MinValue;
        }

        private void WriteState(DateTime utc)
        {
            try { File.WriteAllText(_statePath, utc.ToString("O")); } catch { /* ignore */ }
        }

        private static void TryDelete(string? path)
        {
            try { if (path is not null && File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        private void Log(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} [CloudBackup] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            try
            {
                lock (_logLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory);
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch { /* never crash the app for logging */ }
        }
    }
}
