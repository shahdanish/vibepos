using System.Configuration;
using System.Data;
using System.Windows; // WPF Application
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using POSApp.Data;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.Repositories;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;
using POSApp.UI.Converters;

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
        
        // Add ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<WholeSaleViewModel>();
        services.AddTransient<SaleReturnViewModel>();
        services.AddTransient<SalesReportViewModel>();
        services.AddTransient<ProductManagementViewModel>();
        services.AddTransient<CategoryManagementViewModel>();
        
        // Add Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SaleWindow>();
        services.AddTransient<WholeSaleWindow>();
        services.AddTransient<SaleReturnWindow>();
        services.AddTransient<SalesReportWindow>();
        services.AddTransient<ProductManagementWindow>();
        services.AddTransient<CategoryManagementWindow>();
        
        // Build service provider
        Services = services.BuildServiceProvider();

        // Ensure database is created
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
        }

        // Show login window first
        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();

        base.OnStartup(e);
    }
}

