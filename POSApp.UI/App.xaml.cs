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

    protected override void OnStartup(StartupEventArgs e)
    {
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
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IHoldSaleRepository, HoldSaleRepository>();
        services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
        // New feature repositories
        services.AddScoped<IDailySalesSummaryRepository, DailySalesSummaryRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IPharmacyRepository, PharmacyRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

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
        services.AddTransient<PharmacySaleViewModel>();
        services.AddTransient<PharmacySaleWindow>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<UserFormViewModel>();
        services.AddTransient<RoleManagementViewModel>();
        services.AddTransient<RoleFormViewModel>();
        services.AddTransient<UserManagementWindow>();
        services.AddTransient<UserFormDialog>();
        services.AddTransient<RoleManagementWindow>();
        services.AddTransient<RoleFormDialog>();

        // Add Sync Services (Firebase background sync)
        services.AddSingleton<ISyncService, FirebaseSyncService>();
        
        // Add new services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        // Build service provider
        Services = services.BuildServiceProvider();

        // Ensure database is created and migrated (applies new tables/columns)
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();
        }

        // Load per-client configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var credentialsPath = config["Firebase:CredentialsPath"];

        // Resolve absolute path if relative (relative = next to the .exe)
        if (!string.IsNullOrWhiteSpace(credentialsPath) && !Path.IsPathRooted(credentialsPath))
            credentialsPath = Path.Combine(AppContext.BaseDirectory, credentialsPath);

        // Initialize Firebase sync background service
        var syncService = Services.GetRequiredService<ISyncService>() as FirebaseSyncService;
        syncService?.Initialize(credentialsPath);

        // Show login window first
        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();

        base.OnStartup(e);
    }
}

