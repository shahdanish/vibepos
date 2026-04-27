using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly AppDbContext _context;

        public SettingsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key);
            
            return setting?.Value;
        }

        public async Task<T?> GetSettingAsync<T>(string key)
        {
            var value = await GetSettingAsync(key);
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

        public async Task SetSettingAsync(string key, string value)
        {
            var setting = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key);
            
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
            
            await _context.SaveChangesAsync();
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            await SetSettingAsync(key, value?.ToString() ?? string.Empty);
        }

        public async Task<ApplicationSetting?> GetSettingDetailAsync(string key)
        {
            return await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key);
        }

        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync()
        {
            return await _context.ApplicationSettings.ToListAsync();
        }
    }
}
