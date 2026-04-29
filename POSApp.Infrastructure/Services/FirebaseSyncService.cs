using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;
using System.Text.Json;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// One-way sync engine: reads pending SyncLog entries and pushes
    /// entity data to Firebase Firestore. The POS stays fully offline —
    /// Firebase is a read-only mirror for backup/dashboard viewing.
    /// </summary>
    public sealed class FirebaseSyncService : ISyncService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectivityDetector _connectivityDetector;
        private FirestoreDb? _firestoreDb;
        private readonly CancellationTokenSource _cts = new();
        private Task? _syncLoopTask;
        private int _pendingCount;
        private bool _disposed;
        private readonly string _logPath;
        private readonly string _fallbackLogPath;
        private readonly object _logLock = new();

        /// <summary>
        /// Simple file logger — writes timestamped lines to sync.log next to the .exe.
        /// Check this file to see if background sync is working.
        /// </summary>
        private void Log(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} [FirebaseSync] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            try
            {
                lock (_logLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory);
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Best-effort fallback: base directory might be read-only under certain hosts.
                try
                {
                    lock (_logLock)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_fallbackLogPath) ?? AppContext.BaseDirectory);
                        File.AppendAllText(_fallbackLogPath, line + Environment.NewLine);
                    }
                }
                catch { /* never crash the app */ }
            }
        }

        public bool IsOnline => _connectivityDetector.IsOnline;
        public int PendingCount => _pendingCount;

        public event EventHandler<bool>? ConnectivityChanged;
        public event EventHandler<SyncResult>? SyncCompleted;

        public FirebaseSyncService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logPath = Path.Combine(AppContext.BaseDirectory, "sync.log");
            _fallbackLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "POSApp",
                "sync.log");
            _connectivityDetector = new ConnectivityDetector(TimeSpan.FromSeconds(30));
            _connectivityDetector.ConnectivityChanged += (_, isOnline) =>
            {
                Log($"Connectivity changed: {(isOnline ? "ONLINE" : "OFFLINE")}");
                ConnectivityChanged?.Invoke(this, isOnline);
                // When coming online, trigger an immediate sync
                if (isOnline) _ = SyncPendingChangesAsync(_cts.Token);
            };
        }

        /// <summary>
        /// Initialize Firebase with the credentials file and start background loops.
        /// Call this once at app startup after DI is configured.
        /// </summary>
        public void Initialize(string? credentialsPath = null, string? projectId = null)
        {
            Log("=== FirebaseSyncService initializing ===");
            Log($"AppContext.BaseDirectory={AppContext.BaseDirectory}");
            Log($"Environment.CurrentDirectory={Environment.CurrentDirectory}");
            Log($"Sync log path: {_logPath}");
            Log($"Fallback sync log path: {_fallbackLogPath}");
            
            try
            {
                var credPath = credentialsPath ?? Path.Combine(AppContext.BaseDirectory, "firebase-credentials.json");
                Log($"Looking for credentials at: {credPath}");
                Log($"Credentials file exists: {File.Exists(credPath)}");

                if (File.Exists(credPath))
                {
                    // Auto-read project_id from the service account key if not explicitly provided
                    var effectiveProjectId = projectId ?? ReadProjectIdFromCredentials(credPath);
                    Log($"Project ID: {effectiveProjectId}");

                    var builder = new FirestoreDbBuilder
                    {
                        ProjectId = effectiveProjectId,
                        CredentialsPath = credPath
                    };
                    _firestoreDb = builder.Build();
                    Log($"Firestore initialized successfully");
                }
                else
                {
                    Log("⚠ No credentials file found — sync disabled (app works offline)");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Failed to initialize: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Log($"   Inner: {ex.InnerException.Message}");
                // App still works offline — Firebase is optional
            }

            Log($"Connectivity detector starting (checks every 30s)");
            // Start connectivity detection
            _connectivityDetector.Start();

            Log($"Background sync loop starting (syncs every 60s when online)");
            // Start background sync loop (every 60 seconds when online)
            _syncLoopTask = Task.Run(SyncLoopAsync);
            
            Log("=== Initialization complete ===");
        }

        /// <summary>
        /// Force an immediate sync — useful for testing or manual triggers.
        /// </summary>
        public async Task ForceSyncAsync(CancellationToken ct = default)
        {
            Log($"Force sync triggered — online={_connectivityDetector.IsOnline}, db={(_firestoreDb != null ? "OK" : "NULL")}");
            await SyncPendingChangesAsync(ct);
        }

        public async Task LogChangeAsync(string entityType, int entityId, string operation, CancellationToken ct = default)
        {
            // SyncLog entries are auto-created by SyncLogInterceptor.
            // This method exists for manual logging if ever needed.
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SyncLogs.Add(new SyncLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = operation,
                CreatedAt = DateTime.Now,
                SyncedAt = null
            });
            await db.SaveChangesAsync(ct);
        }

        public async Task SyncPendingChangesAsync(CancellationToken ct = default)
        {
            if (_firestoreDb is null)
            {
                Log("⚠ Sync skipped: Firestore not initialized (check credentials)");
                return;
            }
            if (!_connectivityDetector.IsOnline)
            {
                Log("⚠ Sync skipped: offline");
                return;
            }

            int pushed = 0;
            int failed = 0;
            string? errorMsg = null;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Get all pending (unsynced) log entries, oldest first
                var pendingLogs = await db.SyncLogs
                    .Where(s => s.SyncedAt == null)
                    .OrderBy(s => s.CreatedAt)
                    .Take(100) // batch limit per cycle
                    .ToListAsync(ct);

                Log($"Pending changes: {pendingLogs.Count}");

                if (pendingLogs.Count == 0)
                {
                    _pendingCount = 0;
                    Log("No pending changes to sync");
                    return;
                }

                // Deduplicate: for each entity, only push the latest version
                var latestPerEntity = pendingLogs
                    .GroupBy(s => new { s.EntityType, s.EntityId })
                    .Select(g => g.Last()) // last entry = most recent state
                    .ToList();

                foreach (var logEntry in latestPerEntity)
                {
                    try
                    {
                        var entityData = await LoadEntityAsync(db, logEntry.EntityType, logEntry.EntityId, ct);

                        if (entityData is not null)
                        {
                            // Push to Firestore: collection = EntityType, document = EntityId
                            var docRef = _firestoreDb.Collection(logEntry.EntityType.ToLowerInvariant())
                                                     .Document(logEntry.EntityId.ToString());

                            await docRef.SetAsync(entityData, SetOptions.MergeAll, ct);
                        }

                        // Mark ALL log entries for this entity as synced (not just the latest)
                        var relatedLogs = pendingLogs
                            .Where(s => s.EntityType == logEntry.EntityType && s.EntityId == logEntry.EntityId);

                        foreach (var related in relatedLogs)
                        {
                            related.SyncedAt = DateTime.Now;
                        }

                        Log($"Pushed {logEntry.EntityType}/{logEntry.EntityId} ({logEntry.Operation})");
                        pushed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errorMsg = ex.Message;
                        Log($"Failed to push {logEntry.EntityType}/{logEntry.EntityId}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync(ct);
                _pendingCount = await db.SyncLogs.CountAsync(s => s.SyncedAt == null, ct);
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                Log($"Sync cycle failed: {ex.Message}");
            }

            Log($"Sync result: pushed={pushed}, failed={failed}{(errorMsg != null ? ", error=" + errorMsg : "")}");

            SyncCompleted?.Invoke(this, new SyncResult
            {
                Success = failed == 0,
                PushedCount = pushed,
                FailedCount = failed,
                ErrorMessage = errorMsg
            });
        }

        /// <summary>
        /// Load an entity from SQLite and convert to a Firestore-compatible dictionary.
        /// </summary>
        private static async Task<Dictionary<string, object>?> LoadEntityAsync(
            AppDbContext db, string entityType, int entityId, CancellationToken ct)
        {
            return entityType switch
            {
                nameof(Product) => await ProductToDict(db, entityId, ct),
                nameof(Sale) => await SaleToDict(db, entityId, ct),
                nameof(SaleItem) => await SaleItemToDict(db, entityId, ct),
                nameof(Category) => await CategoryToDict(db, entityId, ct),
                nameof(Customer) => await CustomerToDict(db, entityId, ct),
                nameof(User) => await UserToDict(db, entityId, ct),
                nameof(ApplicationSetting) => await SettingToDict(db, entityId, ct),
                _ => null
            };
        }

        private static async Task<Dictionary<string, object>?> ProductToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var p = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (p is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = p.Id,
                ["productId"] = p.ProductId,
                ["barcode"] = p.Barcode,
                ["productName"] = p.ProductName,
                ["costPrice"] = (double)p.CostPrice,
                ["unitPrice"] = (double)p.UnitPrice,
                ["wholesalePrice"] = (double)p.WholesalePrice,
                ["stock"] = p.Stock,
                ["minStockThreshold"] = p.MinStockThreshold,
                ["profitMarginPercentage"] = (double)p.ProfitMarginPercentage,
                ["isDeleted"] = p.IsDeleted,
                ["rack"] = p.Rack ?? "",
                ["categoryId"] = p.CategoryId ?? 0,
                ["createdDate"] = p.CreatedDate.ToString("O"),
                ["modifiedDate"] = p.ModifiedDate?.ToString("O") ?? "",
            };
        }

        private static async Task<Dictionary<string, object>?> SaleToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var s = await db.Sales.Include(s => s.SaleItems).FirstOrDefaultAsync(s => s.Id == id, ct);
            if (s is null) return null;
            var dict = new Dictionary<string, object>
            {
                ["id"] = s.Id,
                ["invoiceNumber"] = s.InvoiceNumber,
                ["saleDate"] = s.SaleDate.ToString("O"),
                ["saleType"] = s.SaleType,
                ["paymentType"] = s.PaymentType,
                ["customerId"] = s.CustomerId ?? 0,
                ["customerName"] = s.CustomerName,
                ["mobileNumber"] = s.MobileNumber ?? "",
                ["billNote"] = s.BillNote ?? "",
                ["discountOnProducts"] = (double)s.DiscountOnProducts,
                ["discountOnBill"] = (double)s.DiscountOnBill,
                ["totalBill"] = (double)s.TotalBill,
                ["receiveCash"] = (double)s.ReceiveCash,
                ["balance"] = (double)s.Balance,
                ["autoPrinted"] = s.AutoPrinted,
                ["createdDate"] = s.CreatedDate.ToString("O"),
            };

            // SaleItems as a nested array within the Sale document
            var items = s.SaleItems.Select(si => new Dictionary<string, object>
            {
                ["id"] = si.Id,
                ["productId"] = si.ProductId,
                ["productName"] = si.ProductName,
                ["quantity"] = si.Quantity,
                ["costPrice"] = (double)si.CostPrice,
                ["unitPrice"] = (double)si.UnitPrice,
                ["discountPercent"] = (double)si.DiscountPercent,
                ["total"] = (double)si.Total,
            }).ToList();

            dict["items"] = items;
            return dict;
        }

        private static async Task<Dictionary<string, object>?> SaleItemToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var si = await db.SaleItems.FirstOrDefaultAsync(si => si.Id == id, ct);
            if (si is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = si.Id,
                ["saleId"] = si.SaleId,
                ["productId"] = si.ProductId,
                ["productName"] = si.ProductName,
                ["quantity"] = si.Quantity,
                ["costPrice"] = (double)si.CostPrice,
                ["unitPrice"] = (double)si.UnitPrice,
                ["discountPercent"] = (double)si.DiscountPercent,
                ["total"] = (double)si.Total,
            };
        }

        private static async Task<Dictionary<string, object>?> CategoryToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var c = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (c is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = c.Id,
                ["name"] = c.Name,
                ["description"] = c.Description ?? "",
                ["createdDate"] = c.CreatedDate.ToString("O"),
                ["modifiedDate"] = c.ModifiedDate?.ToString("O") ?? "",
            };
        }

        private static async Task<Dictionary<string, object>?> CustomerToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var c = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (c is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = c.Id,
                ["customerId"] = c.CustomerId,
                ["name"] = c.Name,
                ["phone"] = c.Phone ?? "",
                ["cellNo"] = c.CellNo ?? "",
                ["address"] = c.Address ?? "",
                ["preBalance"] = (double)c.PreBalance,
                ["createdDate"] = c.CreatedDate.ToString("O"),
                ["modifiedDate"] = c.ModifiedDate?.ToString("O") ?? "",
            };
        }

        private static async Task<Dictionary<string, object>?> UserToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var u = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (u is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = u.Id,
                ["username"] = u.Username,
                // SECURITY: Never push password hashes to Firestore
                ["role"] = u.Role,
                ["isActive"] = u.IsActive,
                ["createdDate"] = u.CreatedDate.ToString("O"),
                ["modifiedDate"] = u.ModifiedDate?.ToString("O") ?? "",
                ["lastLoginDate"] = u.LastLoginDate?.ToString("O") ?? "",
            };
        }

        private static async Task<Dictionary<string, object>?> SettingToDict(AppDbContext db, int id, CancellationToken ct)
        {
            var a = await db.ApplicationSettings.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (a is null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = a.Id,
                ["key"] = a.Key,
                ["value"] = a.Value,
                ["description"] = a.Description ?? "",
                ["createdDate"] = a.CreatedDate.ToString("O"),
                ["modifiedDate"] = a.ModifiedDate?.ToString("O") ?? "",
            };
        }

        /// <summary>
        /// Reads the project_id field from a Firebase service account JSON key file.
        /// This eliminates the need to hard-code the project ID anywhere.
        /// </summary>
        private static string ReadProjectIdFromCredentials(string credPath)
        {
            var json = File.ReadAllText(credPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("project_id").GetString()
                   ?? throw new InvalidOperationException("project_id not found in credentials file");
        }

        /// <summary>
        /// Background loop: sync every 60 seconds when online.
        /// </summary>
        private async Task SyncLoopAsync()
        {
            Log("Background sync loop started (every 60s)");
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), _cts.Token);
                }
                catch (OperationCanceledException) { break; }

                var online = _connectivityDetector.IsOnline;
                var firestoreOk = _firestoreDb is not null;
                Log($"Sync loop tick: online={online}, firestore={(firestoreOk ? "OK" : "NULL")}");

                if (online && firestoreOk)
                    await SyncPendingChangesAsync(_cts.Token);
                else
                    Log("Skipping sync on this tick (waiting for connectivity/initialization).");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _connectivityDetector.Dispose();
            _cts.Dispose();
        }
    }
}
