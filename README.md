# POSApp - Point of Sale Application

A clean architecture POS application built with .NET 9.0 and WPF.

## 📁 Project Structure

```
POSApp.sln
├── POSApp.Core           → Entities, Interfaces, Business Logic
├── POSApp.Data           → Database (EF Core with SQLite)
├── POSApp.Infrastructure → Repositories, File IO, Services
└── POSApp.UI             → WPF front-end
```

## 🏗️ Architecture

This project follows **Clean Architecture** principles:

- **POSApp.Core**: Contains domain entities and business logic (no dependencies)
- **POSApp.Data**: Database context and EF Core configurations
- **POSApp.Infrastructure**: Implementation of repositories and services
- **POSApp.UI**: WPF presentation layer with MVVM pattern

### Project Dependencies

```
POSApp.UI
  ├── → POSApp.Core
  └── → POSApp.Infrastructure

POSApp.Infrastructure
  ├── → POSApp.Core
  └── → POSApp.Data

POSApp.Data
  └── → POSApp.Core
```

## 🗃️ Database

- **Database**: SQLite
- **ORM**: Entity Framework Core 9.0.10
- **Database File**: `posapp.db` (created automatically in the application directory)

### Current Entities

- **Sale**: Represents a sales transaction
  - Id (Primary Key)
  - SaleDate
  - TotalAmount
  - CustomerName
  - Items
  - PaymentMethod

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or VS Code

### Building the Project

```bash
dotnet build
```

### Running the Application

```bash
dotnet run --project POSApp.UI
```

## 📦 NuGet Packages

### POSApp.Data
- Microsoft.EntityFrameworkCore (9.0.10)
- Microsoft.EntityFrameworkCore.Sqlite (9.0.10)
- Microsoft.EntityFrameworkCore.Tools (9.0.10)

### POSApp.UI
- Microsoft.Extensions.DependencyInjection (9.0.10)

## 🔧 Configuration

The application uses dependency injection configured in `App.xaml.cs`:

- DbContext is registered and configured
- Database is automatically created on application startup
- Service provider is accessible via `App.Services`

## 📝 Next Steps

1. Create repository interfaces in `POSApp.Core`
2. Implement repositories in `POSApp.Infrastructure`
3. Create ViewModels for MVVM pattern in `POSApp.UI`
4. Design the UI in XAML
5. Add business logic and validation
6. Implement additional entities (Products, Customers, etc.)

## 📄 License

This project is licensed under the MIT License.
