using System.IO;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// The shop settings files are not inside posapp.db, so a backup restored onto a new PC
    /// used to come back with default (Pakistani) settings. The mirror keeps a copy in the DB.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class SharedSettingsMirrorTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();
        private readonly ServiceProvider _services;

        public SharedSettingsMirrorTests()
        {
            _dir.NewMigratedContext().Dispose();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>();
            services.AddScoped<ISettingsRepository, SettingsRepository>();
            _services = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _services.Dispose();
            _dir.Dispose();
        }

        private SharedSettingsMirror NewMirror() => new(_services.GetRequiredService<IServiceScopeFactory>());

        private async Task<string?> StoredCopy(string file)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISettingsRepository>()
                .GetSettingAsync(SharedSettingsMirror.Files[file]);
        }

        [Fact]
        public async Task RestoredOntoANewPc_TheSettingsFileComesBack()
        {
            var path = SharedSettingsFiles.PathOf(SharedSettingsFiles.Region);
            var usJson = RegionSettingsStore.Serialize(RegionSettingsData.UnitedStates());
            File.WriteAllText(path, usJson);

            var mirror = NewMirror();
            Assert.Empty(await mirror.SyncAsync());                 // copies the file into the database
            Assert.Equal(usJson, await StoredCopy(SharedSettingsFiles.Region));

            File.Delete(path);                                       // "new PC": only the database came across
            var restored = await mirror.SyncAsync();

            Assert.Equal(new[] { SharedSettingsFiles.Region }, restored);
            Assert.Equal(usJson, File.ReadAllText(path));
            RegionSettingsStore.Reload();
            Assert.True(RegionCodes.IsUnitedStates(RegionSettingsStore.Current.RegionCode));
        }

        [Fact]
        public async Task TheFileOnThisPcWins_AndSavesRefreshTheCopy()
        {
            var path = SharedSettingsFiles.PathOf(SharedSettingsFiles.Region);
            File.WriteAllText(path, RegionSettingsStore.Serialize(RegionSettingsData.Pakistan()));
            var mirror = NewMirror();
            await mirror.SyncAsync();

            var usJson = RegionSettingsStore.Serialize(RegionSettingsData.UnitedStates());
            File.WriteAllText(path, usJson);                         // the shop saved new settings
            await mirror.StoreAsync(SharedSettingsFiles.Region);

            Assert.Equal(usJson, await StoredCopy(SharedSettingsFiles.Region));
            Assert.Equal(usJson, File.ReadAllText(path));
        }

        [Fact]
        public async Task NothingSavedYet_NothingIsWritten()
        {
            Assert.Empty(await NewMirror().SyncAsync());
            Assert.False(File.Exists(SharedSettingsFiles.PathOf(SharedSettingsFiles.ReceiptBranding)));
            Assert.Null(await StoredCopy(SharedSettingsFiles.ReceiptBranding));
        }
    }
}
