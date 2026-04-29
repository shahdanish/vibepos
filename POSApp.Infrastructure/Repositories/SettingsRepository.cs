using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class SettingsRepository : ISettingsRepository
    {
        private readonly AppDbContext _context;

        public SettingsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
        {
            var setting = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key, ct);

            return setting?.Value;
        }

        public async Task<T?> GetSettingAsync<T>(string key, CancellationToken ct = default)
        {
            var value = await GetSettingAsync(key, ct);
            if (value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
        {
            var setting = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key, ct);

            if (setting != null)
            {
                setting.Value = value;
                setting.ModifiedDate = DateTime.Now;
                _context.Entry(setting).State = EntityState.Modified;
            }
            else
            {
                setting = new ApplicationSetting
                {
                    Key = key,
                    Value = value,
                    CreatedDate = DateTime.Now
                };
                _context.ApplicationSettings.Add(setting);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task SetSettingAsync<T>(string key, T value, CancellationToken ct = default)
        {
            await SetSettingAsync(key, value?.ToString() ?? string.Empty, ct);
        }

        public async Task<ApplicationSetting?> GetSettingDetailAsync(string key, CancellationToken ct = default)
        {
            return await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key, ct);
        }

        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(CancellationToken ct = default)
        {
            return await _context.ApplicationSettings.ToListAsync(ct);
        }
    }
}
