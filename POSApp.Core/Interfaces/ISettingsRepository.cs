using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISettingsRepository
    {
        Task<string?> GetSettingAsync(string key);
        Task<T?> GetSettingAsync<T>(string key);
        Task SetSettingAsync(string key, string value);
        Task SetSettingAsync<T>(string key, T value);
        Task<ApplicationSetting?> GetSettingDetailAsync(string key);
        Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync();
    }
}
