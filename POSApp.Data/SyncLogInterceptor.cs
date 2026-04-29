using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POSApp.Core.Entities;

namespace POSApp.Data
{
    /// <summary>
    /// EF Core interceptor that automatically creates SyncLog entries
    /// whenever SaveChangesAsync is called. Zero code changes needed
    /// in existing repositories or ViewModels.
    /// 
    /// CRITICAL: All SyncLog operations are wrapped in try/catch so that
    /// sync-tracking failures NEVER crash the real business operation.
    /// </summary>
    public sealed class SyncLogInterceptor : ISaveChangesInterceptor
    {
        // EF Core's e.Metadata.Name is the CLR "short" entity name (e.g. "Product"),
        // so we must compare against short names as well.
        private static readonly HashSet<string> TrackedEntityNames =
        [
            typeof(Product).Name,
            typeof(Sale).Name,
            typeof(SaleItem).Name,
            typeof(Category).Name,
            typeof(Customer).Name,
            typeof(User).Name,
            typeof(ApplicationSetting).Name,
        ];

        // Cached flag: once we confirm SyncLogs table exists, skip future checks.
        // Volatile for thread-safety across the background sync loop.
        private static volatile bool _syncLogTableExists;

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken ct = default)
        {
            try
            {
                var context = eventData.Context;
                if (context is null) return ValueTask.FromResult(result);

                // Skip SyncLog tracking if the table hasn't been created yet
                if (!_syncLogTableExists && !IsSyncLogTablePresent(context))
                    return ValueTask.FromResult(result);

                var changedEntries = context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added
                             || e.State == EntityState.Modified
                             || e.State == EntityState.Deleted)
                    .Where(e => TrackedEntityNames.Contains(e.Metadata.Name))
                    .ToList();

                foreach (var entry in changedEntries)
                {
                    var entityType = entry.Metadata.ClrType.Name;
                    var entityId = entry.Properties
                        .FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue;

                    if (entityId is not int id) continue;

                    var operation = entry.State switch
                    {
                        EntityState.Added => "Create",
                        EntityState.Modified => "Update",
                        EntityState.Deleted => "Delete",
                        _ => "Unknown"
                    };

                    // Add SyncLog entry directly to the context so it gets saved in the same transaction
                    context.Set<SyncLog>().Add(new SyncLog
                    {
                        EntityType = entityType,
                        EntityId = id,
                        Operation = operation,
                        CreatedAt = DateTime.Now,
                        SyncedAt = null // pending
                    });
                }
            }
            catch (Exception ex)
            {
                // NEVER let SyncLog failures crash the real business operation
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncLogInterceptor] Non-critical error: {ex.Message}");
            }

            return ValueTask.FromResult(result);
        }

        /// <summary>
        /// Check if the SyncLogs table exists in the SQLite database.
        /// Uses a lightweight PRAGMA query — runs only until the first successful check.
        /// </summary>
        private static bool IsSyncLogTablePresent(DbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='SyncLogs'";

                var result = command.ExecuteScalar();
                var exists = result is long count && count > 0;

                if (exists) _syncLogTableExists = true;
                return exists;
            }
            catch
            {
                return false;
            }
        }

        // Synchronous overload — not used but required by interface
        public InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            return result;
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result) => result;
        public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) => ValueTask.FromResult(result);
        public void SaveChangesFailed(DbContextErrorEventData eventData) { }
        public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default) => Task.CompletedTask;
    }
}
