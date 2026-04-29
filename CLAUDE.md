# POSApp — Development Instructions

## Architecture
This project follows **Clean Architecture** principles and uses the **MVVM (Model-View-ViewModel)** pattern for the UI.

- **POSApp.Core**: Contains Domain Entities and Repository/Service Interfaces. No external dependencies.
- **POSApp.Data**: EF Core implementation, DbContext, and Repository implementations.
- **POSApp.Infrastructure**: External services like direct printing and hardware integration.
- **POSApp.UI**: WPF Application containing Views (XAML) and ViewModels.
- **POSApp.Tests**: Unit tests using xUnit and Moq.

## Tech Stack
- **Runtime**: .NET 10.0-windows
- **UI Framework**: WPF
- **Database**: SQLite with EF Core 9.0
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Testing**: xUnit, Moq, Coverlet

## Development Conventions

### Naming & Structure
- **ViewModels**: Located in `POSApp.UI/ViewModels`. Must inherit from `ViewModelBase`.
- **Views**: Located in `POSApp.UI/Views`.
- **Interfaces**: Defined in `POSApp.Core/Interfaces`, prefixed with `I`.
- **Entities**: Defined in `POSApp.Core/Entities`.

### UI/UX Rules
- **Small Screen Optimization**: All main windows must include `ScrollViewers` to support low-resolution (LED) screens.
- **Direct Printing**: Use the `DirectPrintService` in `POSApp.Infrastructure` for thermal printing without dialogs.
- **Input Handling**: Barcode scans should automatically clear the input field after processing.

### Data Access
- Use **Repository Pattern** for all database operations.
- Migrations are managed in the `POSApp.Data` project.

## Build & Test Commands
- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Run**: `dotnet run --project POSApp.UI`

## Skills Loaded
- clean-architecture
- modern-csharp
- ef-core
- testing
- vertical-slice
