using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POSApp.Core.Entities;
using POSApp.Data;
using POSApp.Infrastructure.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// Hard rule: a cloud backup taken by the previous release must restore on this one. This
    /// runs the real snapshot, encode, decode, verify, schema-guard, restore and start-up upgrade
    /// steps — everything except the Firestore transport, so no client's cloud project is touched.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class CloudBackupRoundTripTests : IDisposable
    {
        private const string PreviousReleaseSchema = "20260709172930_PurchaseItemDecimalQuantity";
        private readonly List<TempDataDirectory> _dirs = new();

        public void Dispose()
        {
            // Newest first, so each folder puts back the one that was active before it.
            for (var i = _dirs.Count - 1; i >= 0; i--) _dirs[i].Dispose();
        }

        private TempDataDirectory NewPc()
        {
            var dir = new TempDataDirectory();
            _dirs.Add(dir);
            return dir;
        }

        /// <summary>What the old shop PC uploads: a snapshot of a previous-release database.</summary>
        private async Task<(CloudBackupPayload.Encoded payload, string schema)> BackUpPreviousReleaseShopAsync()
        {
            var oldPc = NewPc();
            using (var db = new AppDbContext())
            {
                db.GetService<IMigrator>().Migrate(PreviousReleaseSchema);
                var sale = new Sale { InvoiceNumber = "11050", SaleDate = new DateTime(2026, 9, 1, 10, 0, 0), TotalBill = 46, CustomerName = "Ahmed" };
                sale.SaleItems.Add(new SaleItem { ProductId = "101124", ProductName = "Glycerin 25gm", Quantity = 2, UnitPrice = 23, Total = 46 });
                db.Sales.Add(sale);
                db.SaveChanges();
            }

            // Same steps as CloudBackupService.BackupToCloudAsync, minus the upload.
            var snapshot = await new DatabaseBackupService().CreateBackupAsync(Path.Combine(oldPc.Path, "snap"));
            SqliteConnection.ClearAllPools();
            var payload = CloudBackupPayload.Encode(await File.ReadAllBytesAsync(snapshot), chunkCharSize: 4_000);
            return (payload, PreviousReleaseSchema);
        }

        [Fact]
        public async Task PreviousReleaseBackup_RestoresOntoThisVersion_AndUpgrades()
        {
            var (payload, schema) = await BackUpPreviousReleaseShopAsync();
            Assert.True(payload.Chunks.Count > 1, "use several chunks so reassembly order is exercised");

            // The new PC already runs this version, with its own (empty) database.
            var newPc = NewPc();
            using (var fresh = newPc.NewMigratedContext())
            {
                // Schema guard: an older backup is accepted.
                Assert.True(CloudBackupPayload.IsSchemaKnown(fresh.Database.GetMigrations(), schema));
            }

            // Download → decode → verify → restore, as RestoreFromCloudAsync does.
            var raw = CloudBackupPayload.Decode(payload.Chunks);
            Assert.True(CloudBackupPayload.Verify(raw, payload.SizeBytes, payload.Sha256));
            var downloaded = Path.Combine(newPc.Path, "restore.db");
            await File.WriteAllBytesAsync(downloaded, raw);
            SqliteConnection.ClearAllPools();
            await new DatabaseBackupService().RestoreFromBackupAsync(downloaded);

            // The user restarts the app: start-up upgrades the restored, older database.
            using var db = new AppDbContext();
            var outcome = DatabaseMigrator.MigrateWithBackup(db);

            Assert.Contains(outcome.Applied, m => m.EndsWith("_AddLookupIndexes"));
            Assert.NotNull(outcome.BackupPath);   // the restored data was copied aside before upgrading
            var sale = Assert.Single(db.Sales.Include(s => s.SaleItems).ToList());
            Assert.Equal("11050", sale.InvoiceNumber);
            Assert.Equal("Ahmed", sale.CustomerName);
            Assert.Equal(46m, Assert.Single(sale.SaleItems).Total);
        }

        [Fact]
        public async Task DamagedOrForeignBackups_AreRefused()
        {
            var (payload, _) = await BackUpPreviousReleaseShopAsync();

            // Any change to the restored file is caught by the size + SHA-256 check.
            var raw = CloudBackupPayload.Decode(payload.Chunks);
            Assert.True(CloudBackupPayload.Verify(raw, payload.SizeBytes, payload.Sha256));
            var altered = (byte[])raw.Clone();
            altered[altered.Length / 2] ^= 0xFF;
            Assert.False(CloudBackupPayload.Verify(altered, payload.SizeBytes, payload.Sha256));

            // A chunk corrupted in transit (inside the compressed data, not the gzip trailer)
            // either cannot be decompressed or fails the checksum.
            var tampered = payload.Chunks.ToList();
            var chars = tampered[0].ToCharArray();
            var mid = chars.Length / 2;
            chars[mid] = chars[mid] == 'A' ? 'B' : 'A';
            tampered[0] = new string(chars);
            var damagedIsRefused = true;
            try { damagedIsRefused = !CloudBackupPayload.Verify(CloudBackupPayload.Decode(tampered), payload.SizeBytes, payload.Sha256); }
            catch (InvalidDataException) { }
            Assert.True(damagedIsRefused);

            // A snapshot written by a newer app version is refused by the schema guard.
            using var db = NewPc().NewMigratedContext();
            Assert.False(CloudBackupPayload.IsSchemaKnown(db.Database.GetMigrations(), "20990101000000_FromTheFuture"));
        }
    }
}
