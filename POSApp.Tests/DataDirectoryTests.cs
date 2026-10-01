using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// Tests that point <see cref="AppPaths"/> at a temporary folder via POSAPP_DATA_DIR.
    /// They share process-wide state, so they run in the non-parallel global-state collection.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class DataDirectoryTests : IDisposable
    {
        private readonly string _root;
        private readonly string _dataDir;
        private readonly string? _previous;

        public DataDirectoryTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "posapp-tests-" + Guid.NewGuid().ToString("N"));
            _dataDir = Path.Combine(_root, "data");
            Directory.CreateDirectory(_root);

            _previous = Environment.GetEnvironmentVariable("POSAPP_DATA_DIR");
            Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", _dataDir);
            AppPaths.ResetForTests();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", _previous);
            AppPaths.ResetForTests();
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_root, recursive: true); } catch { /* temp folder */ }
        }

        // ── AppPaths ───────────────────────────────────────────────────────

        [Fact]
        public void DatabasePath_LivesInDataDirectory()
        {
            Assert.Equal(Path.Combine(_dataDir, "posapp.db"), AppPaths.DatabasePath);
            Assert.True(Directory.Exists(_dataDir));
        }

        [Fact]
        public void MigrateLegacyDatabase_CopiesOldDatabaseAndSideFiles()
        {
            var legacyDir = Path.Combine(_root, "old-install");
            Directory.CreateDirectory(legacyDir);
            File.WriteAllText(Path.Combine(legacyDir, "posapp.db"), "db");
            File.WriteAllText(Path.Combine(legacyDir, "posapp.db-wal"), "wal");

            var migrated = AppPaths.MigrateLegacyDatabase(legacyDir);

            Assert.Equal(Path.Combine(legacyDir, "posapp.db"), migrated);
            Assert.Equal("db", File.ReadAllText(AppPaths.DatabasePath));
            Assert.Equal("wal", File.ReadAllText(AppPaths.DatabasePath + "-wal"));
            Assert.True(File.Exists(Path.Combine(legacyDir, "posapp.db")), "legacy copy must be kept");
        }

        [Fact]
        public void MigrateLegacyDatabase_NeverOverwritesExistingDatabase()
        {
            File.WriteAllText(AppPaths.DatabasePath, "current");
            var legacyDir = Path.Combine(_root, "old-install");
            Directory.CreateDirectory(legacyDir);
            File.WriteAllText(Path.Combine(legacyDir, "posapp.db"), "old");

            Assert.Null(AppPaths.MigrateLegacyDatabase(legacyDir));
            Assert.Equal("current", File.ReadAllText(AppPaths.DatabasePath));
        }

        // ── First-run setup against a real migrated database ───────────────

        [Fact]
        public async Task FirstRunSetup_ReplacesDemoLoginsWithOwnerAccount()
        {
            await using (var db = new AppDbContext())
                await db.Database.MigrateAsync();

            await using (var db = new AppDbContext())
            {
                var setup = new FirstRunSetupService(db);
                Assert.False(await setup.IsCompleteAsync());

                await setup.CompleteAsync(new FirstRunSetupRequest("owner", "Owner#2026", KeepSampleProducts: false));
                Assert.True(await setup.IsCompleteAsync());
            }

            await using (var db = new AppDbContext())
            {
                var users = new UserRepository(db);
                Assert.Null(await users.ValidateUserAsync("admin", "admin123"));
                Assert.Null(await users.ValidateUserAsync("ali", "ali443"));
                Assert.Null(await users.ValidateUserAsync("alico", "1"));

                var owner = await users.ValidateUserAsync("owner", "Owner#2026");
                Assert.NotNull(owner);
                Assert.Equal(1, owner!.RoleId);
                Assert.True(PasswordHasher.IsHashed(owner.PasswordHash));

                Assert.Equal(1, await db.Users.CountAsync(u => u.IsActive));
                Assert.False(await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == 1 || p.Id == 2));
            }
        }

        [Fact]
        public async Task FirstRunSetup_CanReuseSeededUsername()
        {
            await using (var db = new AppDbContext())
                await db.Database.MigrateAsync();

            await using (var db = new AppDbContext())
                await new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest("admin", "NewPass1", KeepSampleProducts: true));

            await using (var db = new AppDbContext())
            {
                var users = new UserRepository(db);
                Assert.Null(await users.ValidateUserAsync("admin", "admin123"));
                Assert.NotNull(await users.ValidateUserAsync("admin", "NewPass1"));
                Assert.True(await db.Products.AnyAsync(p => p.Id == 1));
            }
        }

        [Theory]
        [InlineData("ab", "longenough")]
        [InlineData("owner", "short")]
        public async Task FirstRunSetup_RejectsWeakInput(string user, string password)
        {
            await using var db = new AppDbContext();
            await db.Database.MigrateAsync();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest(user, password, false)));
        }

        [Fact]
        public async Task Login_UpgradesLegacyPlainTextPassword()
        {
            await using (var db = new AppDbContext())
                await db.Database.MigrateAsync();

            await using (var db = new AppDbContext())
            {
                var user = await new UserRepository(db).ValidateUserAsync("cashier", "cashier123");
                Assert.NotNull(user);
            }

            await using (var db = new AppDbContext())
            {
                var stored = await db.Users.Where(u => u.Username == "cashier").Select(u => u.PasswordHash).SingleAsync();
                Assert.True(PasswordHasher.IsHashed(stored));
                Assert.NotNull(await new UserRepository(db).ValidateUserAsync("cashier", "cashier123"));
            }
        }
    }
}
