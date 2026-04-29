using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISettingsRepository
    {
        Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
        Task<T?> GetSettingAsync<T>(string key, CancellationToken ct = default);
        Task SetSettingAsync(string key, string value, CancellationToken ct = default);
        Task SetSettingAsync<T>(string key, T value, CancellationToken ct = default);
        Task<ApplicationSetting?> GetSettingDetailAsync(string key, CancellationToken ct = default);
        Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(CancellationToken ct = default);
    }
}
