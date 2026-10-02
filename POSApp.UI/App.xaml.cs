using System.IO;
using System.Windows; // WPF Application
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using POSApp.Data;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.Services;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;
using POSApp.UI.Converters;
using POSApp.Core.Services;

namespace POSApp.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    static App()
    {
        // Money and quantity boxes bind with UpdateSourceTrigger=PropertyChanged. Since .NET 4.5
        // WPF then rewrites the box from the converted value on every keystroke, so typing
        // "12." became "12" and "12.75" ended up as 1275. Keep what the user typed instead
        // (the .NET 4.0 behaviour). Must run before the first TextBox is created.
        FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;
    }

    private static async Task RunAutoBackupAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAutoBackupService>().RunIfDueAsync();
        }
        catch (Exception ex)
        {
            POSApp.UI.Helpers.CrashLog.Write(ex, "Automatic backup");
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Errors nothing else caught are logged and shown, instead of closing the program mid-sale.
        POSApp.UI.Helpers.CrashLog.Install(this);

        // Pre-login dialogs (setup wizard, licence gate) must not end the app when they close.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // This Windows user's accent colour and density, before the first window appears.
        POSApp.UI.Helpers.ThemeManager.Initialize();

        // Older builds kept posapp.db next to the .exe / in the working folder. Under MSIX that
        // folder is read-only, so all data now lives in AppPaths.DataDirectory. Copy once.
        AppPaths.MigrateLegacyDatabase();
        if (AppEnvironment.IsPackaged)
        {
            AppPaths.MigrateLegacySettingsFile(SharedSettingsFiles.ReceiptBranding);
            AppPaths.MigrateLegacySettingsFile(SharedSettingsFiles.Region);
        }

        var services = new ServiceCollection();

        // Add DbContext
        services.AddDbContext<AppDbContext>();

        // Add Repositories
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        // Phase-1 feature repositories
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IHoldSaleRepository, HoldSaleRepository>();
        services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
        // New feature repositories
        services.AddScoped<IDailySalesSummaryRepository, DailySalesSummaryRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<ITaxRepository, TaxRepository>();
        services.AddScoped<IFrontStoreRepository, FrontStoreRepository>();
        services.AddScoped<IAutoBackupService, AutoBackupService>();
        services.AddScoped<IPharmacyRepository, PharmacyRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IMedicalRepRepository, MedicalRepRepository>();
        services.AddScoped<ICallScheduleRepository, CallScheduleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ISalarySlipRepository, SalarySlipRepository>();

        // Add ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<WholeSaleViewModel>();
        services.AddTransient<SaleReturnViewModel>();
        services.AddTransient<SalesReportViewModel>();
        services.AddTransient<ProductManagementViewModel>();
        services.AddTransient<CategoryManagementViewModel>();
        // Phase-1 feature view models
        services.AddTransient<ExpenseViewModel>();
        services.AddTransient<ShiftViewModel>();
        services.AddTransient<CustomerLedgerViewModel>();
        services.AddTransient<DashboardViewModel>();
        // New feature view models
        services.AddTransient<DailySummaryViewModel>();
        services.AddTransient<PurchaseEntryViewModel>();
        services.AddTransient<PurchaseReturnViewModel>();
        services.AddTransient<SupplierManagementViewModel>();
        services.AddTransient<BackupRestoreViewModel>();
        services.AddTransient<PharmacyManagementViewModel>();
        services.AddTransient<PharmacyFormViewModel>();
        services.AddTransient<DoctorManagementViewModel>();
        services.AddTransient<DoctorFormViewModel>();
        services.AddTransient<CallScheduleViewModel>();
        services.AddTransient<MedicalRepFormViewModel>();
        services.AddTransient<EmployeeManagementViewModel>();
        services.AddTransient<EmployeeFormViewModel>();
        services.AddTransient<SalarySlipViewModel>();

        // Add Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SaleWindow>();
        services.AddTransient<WholeSaleWindow>();
        services.AddTransient<SaleReturnWindow>();
        services.AddTransient<SalesReportWindow>();
        services.AddTransient<ProductManagementWindow>();
        services.AddTransient<CategoryManagementWindow>();
        // Phase-1 feature windows
        services.AddTransient<ExpenseWindow>();
        services.AddTransient<ShiftWindow>();
        services.AddTransient<CustomerLedgerWindow>();
        services.AddTransient<DashboardWindow>();
        // New feature windows
        services.AddTransient<DailySummaryWindow>();
        services.AddTransient<PurchaseEntryWindow>();
        services.AddTransient<PurchaseReturnWindow>();
        services.AddTransient<SupplierManagementWindow>();
        services.AddTransient<BackupRestoreWindow>();
        services.AddTransient<PharmacyManagementWindow>();
        services.AddTransient<PharmacyFormDialog>();
        services.AddTransient<DoctorManagementWindow>();
        services.AddTransient<DoctorFormDialog>();
        services.AddTransient<CallScheduleWindow>();
        services.AddTransient<MedicalRepFormDialog>();
        services.AddTransient<PharmacySaleViewModel>();
        services.AddTransient<PharmacySaleWindow>();
        services.AddTransient<EmployeeManagementWindow>();
        services.AddTransient<EmployeeFormDialog>();
        services.AddTransient<SalarySlipWindow>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<UserFormViewModel>();
        services.AddTransient<RoleManagementViewModel>();
        services.AddTransient<RoleFormViewModel>();
        services.AddTransient<UserManagementWindow>();
        services.AddTransient<UserFormDialog>();
        services.AddTransient<RoleManagementWindow>();
        services.AddTransient<RoleFormDialog>();

        // Current-user context (backs service/data-layer permission enforcement)
        services.AddSingleton<ICurrentUserContext, POSApp.UI.Helpers.CurrentUserContext>();

        // NOTE: The old per-record Firebase mirror (FirebaseSyncService) is intentionally
        // NOT registered or started anymore. The app now uses ONLY full-database cloud
        // backup/restore (ICloudBackupService) for disaster recovery, so it no longer
        // pushes product/customer/sale/etc. documents to Firestore.

        // Add new services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        // Full-database cloud backup/restore (disaster recovery)
        services.AddSingleton<ICloudBackupService, CloudBackupService>();

        // Yearly time-limited license enforcement (direct edition only)
        services.AddSingleton<ILicenseService, LicenseService>();

        // Regional formatting (currency, numbers, dates, phone) — the same instance the static
        // Region facade and the XAML converters use.
        services.AddSingleton<IFormatService>(_ => POSApp.UI.Helpers.Region.Format);

        // Copies the shop settings files into the database so backups carry them.
        services.AddSingleton<SharedSettingsMirror>();

        // Edition (direct / Store Lite / Store Pro) and Store first-run setup
        services.AddSingleton<IEditionService, EditionService>();
        services.AddScoped<IFirstRunSetupService, FirstRunSetupService>();
        services.AddTransient<FirstRunSetupWindow>();

        // Build service provider
        Services = services.BuildServiceProvider();

        // Ensure database is created and migrated (applies new tables/columns). An existing
        // database is copied aside first, so a failed or unwanted upgrade can be undone.
        bool databaseExisted = false;
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                databaseExisted = dbContext.Database.GetAppliedMigrations().Any();
                DatabaseMigrator.MigrateWithBackup(dbContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The database could not be upgraded to this version, so the program will close.\n\n" +
                    $"{ex.Message}\n\n" +
                    "If a copy was taken before the upgrade started, it is in:\n" +
                    $"{DatabaseMigrator.BackupDirectory}",
                    "Database Upgrade Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        // Shop settings files <-> their copies in the database. After a backup is restored onto
        // a new PC the files are missing; this writes them back before anything reads them.
        var settingsMirror = Services.GetRequiredService<SharedSettingsMirror>();
        try
        {
            if ((await settingsMirror.SyncAsync()).Count > 0)
            {
                RegionSettingsStore.Reload();
                POSApp.UI.Helpers.ReceiptBranding.Reload();
            }
        }
        catch
        {
            // Never block start-up over the settings copy; the next start retries.
        }
        RegionSettingsStore.Saved += saved => _ = settingsMirror.StoreQuietlyAsync(SharedSettingsFiles.Region);
        POSApp.UI.Helpers.ReceiptBranding.Saved += () => _ = settingsMirror.StoreQuietlyAsync(SharedSettingsFiles.ReceiptBranding);

        // New installs default to the United States. A till that was already in use before
        // that change (the Pakistani shops) keeps what it had: if it never saved its region
        // settings, the Pakistan preset it has always run on is written down for it, once.
        var origin = new InstallOriginResult(InstallOrigin.Upgraded, FirstSeen: false);
        try
        {
            using var scope = Services.CreateScope();
            origin = await scope.ServiceProvider.GetRequiredService<IFirstRunSetupService>()
                .RecordInstallOriginAsync(databaseExisted);
            if (origin.FirstSeen && origin.Origin == InstallOrigin.Upgraded)
                RegionSettingsStore.KeepLegacyDefaultsIfUnset();
            // US: the Pharmacist / Pharmacy Technician roles and the PSE logbook permission, by name.
            if (POSApp.UI.Helpers.Region.IsUnitedStates)
                await scope.ServiceProvider.GetRequiredService<IFrontStoreRepository>().EnsureRolesAsync();
            // New installs back themselves up daily; existing tills switch it on in Backup & Restore.
            if (origin.FirstSeen && origin.Origin == InstallOrigin.New)
                await scope.ServiceProvider.GetRequiredService<IAutoBackupService>().SetEnabledAsync(true);
        }
        catch
        {
            // Treated as an existing install: no setup wizard, nothing changed.
        }

        // Daily automatic backup: now, and every few hours while the till stays open all day.
        _ = RunAutoBackupAsync();
        var backupTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(3) };
        backupTimer.Tick += (_, _) => _ = RunAutoBackupAsync();
        backupTimer.Start();

        var edition = Services.GetRequiredService<IEditionService>();
        await edition.RefreshAsync();

        if (edition.IsInBuild(AppFeature.CloudBackup))
        {
            // Load per-client configuration from appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var credentialsPath = config["Firebase:CredentialsPath"];

            // Resolve absolute path if relative (relative = next to the .exe)
            if (!string.IsNullOrWhiteSpace(credentialsPath) && !Path.IsPathRooted(credentialsPath))
                credentialsPath = Path.Combine(AppContext.BaseDirectory, credentialsPath);

            // Initialize full-database cloud backup (starts the daily auto-backup loop)
            Services.GetRequiredService<ICloudBackupService>().Initialize(credentialsPath);
        }

        // --- First launch asks for the shop details and the owner's login: always on a Store
        //     build, and on the direct build for a database it created (never for a till that
        //     was already in use, whose seeded logins are its real ones) ---------------------
        if (EditionPolicy.IsStore(edition.Edition) || origin.Origin == InstallOrigin.New)
        {
            bool setupDone;
            using (var scope = Services.CreateScope())
                setupDone = await scope.ServiceProvider.GetRequiredService<IFirstRunSetupService>().IsCompleteAsync();

            if (!setupDone)
            {
                using var scope = Services.CreateScope();
                var wizard = scope.ServiceProvider.GetRequiredService<FirstRunSetupWindow>();
                if (wizard.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
        }

        // --- License gate: enforce the yearly period before the app is usable ---------
        if (edition.IsInBuild(AppFeature.YearlyLicence))
        {
            var licenseService = Services.GetRequiredService<ILicenseService>();
            var license = licenseService.CheckLicense();

            if (license.IsBlocked)
            {
                var gate = new LicenseExpiredWindow(licenseService, license);
                gate.ShowDialog();

                if (!gate.Renewed)
                {
                    // No valid renewal entered — do not start the app.
                    Shutdown();
                    return;
                }
            }
            else if (license.State == LicenseState.Expiring)
            {
                MessageBox.Show(
                    $"Your license will expire in {license.DaysRemaining} day(s), on " +
                    $"{POSApp.UI.Helpers.Region.LongDate(license.ExpiryUtc.ToLocalTime())}.\n\n" +
                    $"{licenseService.RenewalContactMessage}\n{licenseService.RenewalInstructions}",
                    "License Expiring Soon", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Show login window first
        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }
}

