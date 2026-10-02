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
    /// Upgrading a database written by the previous release: it is copied aside first, keeps
    /// every row, and gains only additive changes.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class DatabaseMigratorTests : IDisposable
    {
        /// <summary>The newest migration shipped before the US work began.</summary>
        private const string PreviousReleaseSchema = "20260709172930_PurchaseItemDecimalQuantity";

        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static long Scalar(string dbPath, string sql)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        /// <summary>A database exactly as the previous release left it, with one sale on it.</summary>
        private static void CreatePreviousReleaseDatabase()
        {
            using var db = new AppDbContext();
            db.GetService<IMigrator>().Migrate(PreviousReleaseSchema);
            LegacyRows.InsertPreviousReleaseSale(db);
        }

        [Fact]
        public void NewDatabase_IsCreatedWithoutABackup()
        {
            using var db = new AppDbContext();

            var outcome = DatabaseMigrator.MigrateWithBackup(db);

            Assert.NotEmpty(outcome.Applied);
            Assert.Null(outcome.BackupPath);
            Assert.Empty(db.Database.GetPendingMigrations());
        }

        [Fact]
        public void PreviousReleaseDatabase_IsBackedUpFirst_AndKeepsItsData()
        {
            CreatePreviousReleaseDatabase();

            using var db = new AppDbContext();
            var outcome = DatabaseMigrator.MigrateWithBackup(db);

            Assert.Contains(outcome.Applied, m => m.EndsWith("_AddLookupIndexes"));
            Assert.NotNull(outcome.BackupPath);
            Assert.True(File.Exists(outcome.BackupPath));
            Assert.Contains(PreviousReleaseSchema, Path.GetFileName(outcome.BackupPath));

            // The copy is the untouched previous-release database.
            Assert.Equal(1, Scalar(outcome.BackupPath!, "SELECT COUNT(*) FROM Sales"));
            Assert.Equal(0, Scalar(outcome.BackupPath!, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'IX_Sales_SaleDate'"));

            // The upgraded database kept the sale and gained the indexes.
            db.ChangeTracker.Clear();
            Assert.Equal("11050", Assert.Single(db.Sales.Include(s => s.SaleItems).ToList()).InvoiceNumber);
            Assert.Empty(db.Database.GetPendingMigrations());
            foreach (var index in new[] { "IX_Sales_SaleDate", "IX_Sales_InvoiceNumber", "IX_Products_ProductId", "IX_SaleItems_ProductId" })
                Assert.Equal(1, Scalar(Path.Combine(_dir.Path, "posapp.db"), $"SELECT COUNT(*) FROM sqlite_master WHERE name = '{index}'"));
        }

        [Fact]
        public void UpToDateDatabase_IsLeftAlone()
        {
            CreatePreviousReleaseDatabase();
            using var db = new AppDbContext();
            DatabaseMigrator.MigrateWithBackup(db);

            var again = DatabaseMigrator.MigrateWithBackup(db);

            Assert.Empty(again.Applied);
            Assert.Null(again.BackupPath);
            Assert.Single(Directory.GetFiles(DatabaseMigrator.BackupDirectory));
        }

        [Fact]
        public void OnlyTheNewestBackupsAreKept()
        {
            Directory.CreateDirectory(DatabaseMigrator.BackupDirectory);
            for (var i = 0; i < DatabaseMigrator.BackupsToKeep + 2; i++)
            {
                var old = Path.Combine(DatabaseMigrator.BackupDirectory, $"posapp_old{i}.db");
                File.WriteAllText(old, "old");
                File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30 + i));
            }

            CreatePreviousReleaseDatabase();
            using var db = new AppDbContext();
            var outcome = DatabaseMigrator.MigrateWithBackup(db);

            var kept = Directory.GetFiles(DatabaseMigrator.BackupDirectory);
            Assert.Equal(DatabaseMigrator.BackupsToKeep, kept.Length);
            Assert.Contains(outcome.BackupPath, kept);
        }
    }
}
