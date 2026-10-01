# US Expansion — Phase 0 Discovery

Read-only survey of the codebase at `e848f08` (branch `feature/swifttill-store-edition`), 2026-10-01.
Line numbers refer to that commit.

---

## 1. Solution at a glance

| Project | TFM | Role |
|---|---|---|
| `POSApp.Core` | `net10.0` | Entities, repository/service interfaces, pure services (`Edition`, `AppPaths`, `PasswordHasher`). No package deps. |
| `POSApp.Data` | `net10.0` | `AppDbContext`, EF Core 9.0.10 + SQLite, 19 migrations, `SyncLogInterceptor`. |
| `POSApp.Infrastructure` | `net10.0-windows10.0.19041.0` | Repositories, `CloudBackupService` (Firestore), `DatabaseBackupService`, `EditionService` (Store APIs), `LicenseService`, `BarcodeService` (ZXing). |
| `POSApp.UI` | `net10.0-windows10.0.19041.0` | WPF, MVVM (`ViewModelBase`/`SetProperty`, `RelayCommand`), DI via `Microsoft.Extensions.DependencyInjection`, ClosedXML for Excel. |
| `POSApp.Tests` | `net10.0-windows10.0.19041.0` | xUnit + Moq, references every project including UI (STA window tests). |

- **UI tech:** WPF with a single design system (`Themes/DesignSystem.xaml`). It's MVVM, but the business logic (totals, saving, printing) lives *inside* the ViewModels. `SaleViewModel` is 1,097 lines and `PharmacySaleViewModel` is 1,270. There is no checkout/service layer.
- **Editions:** one codebase with three runtime editions (`Core/Services/Edition.cs`): **Direct** (Inno Setup, yearly licence, all features), **StoreLite** and **StorePro** (MSIX "Swifttill", subscription add-on).
- **Build:** `dotnet build POSApp.sln` succeeds with 0 errors and 1 pre-existing warning (NU1510 `Microsoft.Win32.Registry`).
- **Tests:** `dotnet test` → **57 passed, 2 failed (pre-existing, before any change of mine)**:
  - `ProductManagementViewModelTests.ClearForm_ResetsAllProperties`: stale assertion. `ClearForm` now sets `CostPrice = null` (`ProductManagementViewModel.cs:427`) but the test expects `0`.
  - `BusinessSettingsWindowTests.Preview_DoesNotLeakUnsavedSettingsIntoTheApp`: test isolation. `Region` is a mutable static cache shared by test classes running in parallel, and the tests don't set `POSAPP_DATA_DIR`, so they read the developer machine's real `%ProgramData%\ShahJeePOS\region-settings.json` (which is `$` on this PC).
- Existing test files: `RegionSettingsTests`, `BusinessSettingsWindowTests`, `EditionPolicyTests`, `DataDirectoryTests`, `PasswordHasherTests`, `ProductIdGeneratorTests`, `ProductManagementViewModelTests`. There are **no tests for sale totals, returns, shifts, or migrations.**
- `CLAUDE.md` mentions a `DirectPrintService` in Infrastructure. **It does not exist** (see §6).

## 2. Data access, storage, migrations

- **EF Core 9 + SQLite**, Repository pattern (scoped repos, one `AppDbContext` per scope). No Dapper or raw ADO.NET except backup/restore (`Microsoft.Data.Sqlite` `BackupDatabase`).
- **DB file:** `AppPaths.DatabasePath` = `%LocalAppData%\ShahJeePOS\posapp.db`. MSIX redirects this into package storage. It can be relocated with the `POSAPP_DATA_DIR` env var (tests use this, and so can the demo DB). Legacy `posapp.db` beside the exe is *copied* on first start (`AppPaths.MigrateLegacyDatabase`). The DB runs in WAL mode.
- **Migrations:** standard EF migrations, applied by `dbContext.Database.Migrate()` at startup (`App.xaml.cs:172`). Versioning comes from `__EFMigrationsHistory` (each migration runs once). Latest is `20260709172930_PurchaseItemDecimalQuantity`. `dotnet ef migrations has-pending-model-changes` reports **no drift**. `AppDbContext.cs:51` suppresses `PendingModelChangesWarning`.
- ⚠ **No backup is taken before `Migrate()`.** Hard Rule 2 needs this added.
- **Decimals are stored as TEXT** (SQLite has no decimal type, so `HasPrecision` is cosmetic). Example: `TotalBill = '23.0'`. Ordering or comparing decimals in SQL is text-based. Aggregates work, but avoid SQL-side `ORDER BY` on money.
- `SyncLogInterceptor` writes a `SyncLog` row for every insert/update/delete of Product, Sale, SaleItem, Category, Customer, User or ApplicationSetting. It's harmless today, but a 200k-line demo or performance dataset will double its write volume and grow that table.

## 3. Current schema (relevant tables)

| Table | Key columns | Notes |
|---|---|---|
| **Products** | `Id` int PK, `ProductId` string (business code), `Barcode` string (indexed, **not unique**), `ProductName`, `CostPrice`, `UnitPrice`, `WholesalePrice`, **`Stock` int**, `MinStockThreshold`, `ProfitMarginPercentage`, `IsDeleted` (global query filter), `Rack`, **`BatchNo`, `ExpiryDate` (one per product)**, `CategoryId` FK | No tax, NDC, manufacturer, age or PSE fields. `ProductId` is not indexed. |
| **Categories** | `Id`, `Name`, `Description` | Used as "department". Seeded as Medicine and Stationery. |
| **Sales** | `Id`, `InvoiceNumber` string (**no index, not unique**), `SaleDate` (**not indexed**), `SaleType` ("Sale" / "WholeSale" / "Return" / "PharmacySale"), **`PaymentType` single string**, `CustomerId`, `CustomerName`/`Address`/`Phone`/`MobileNumber` (snapshot), `PreBalance`, `BillNote`, `DiscountOnProducts`, `DiscountOnBill`, `TotalBill`, `ReceiveCash`, `Balance`, `AutoPrinted`, `PharmacyId`, `DoctorId` | No tax, cashier, register or shift columns. |
| **SaleItems** | `Id`, `SaleId` FK (cascade), `ProductId` **string**, `ProductName`, `Quantity` decimal(18,3), `Bonus` int, `CostPrice`, `UnitPrice`, `DiscountPercent`, `DiscountType` ("%" / "AMT", legacy "PKR"), `Total` | ⚠ Shadow FK `ProductId1` (int?, see §11). Returns are stored as negative-quantity lines. |
| **Customers** | `Id`, `CustomerId`, `Name`, `Phone`, `CellNo`, `Address`, `PreBalance`, `CurrentBalance` (amount the customer **owes** = "khata"), `LoyaltyPoints`, `TotalPurchases`, `LastPurchaseDate` | No tax-exempt flag. Seeded "Cash" customer, Id 1. |
| **CustomerPayments** | `CustomerId` FK, `AmountPaid`, `PaymentDate`, `Note`, `InvoiceNumber` | Khata repayments. |
| **Suppliers** | `Id`, `SupplierId` (unique), `Name`, `ContactPerson`, `Phone`, `Email`, `Address`, `CurrentBalance`, `PaymentTerms`, `IsActive` | |
| **PurchaseOrders / PurchaseOrderItems** | PO header (`PurchaseNumber` unique, `SupplierId`) + items (`ProductId` string, `Quantity` decimal, `UnitCost`) | Receiving stock = purchase entry. No lot/expiry per receipt. |
| **Shifts** | `Id`, `OpenedAt`, `ClosedAt`, `OpeningBalance`, `ExpectedClosingBalance`, `ActualClosingBalance` (`Difference`/`IsClosed` computed) | No user, register or tender breakdown. |
| **DailySalesSummaries** | `Date`, `OpeningBalance`, `TotalSales`, `TotalExpenses`, `ExpectedClosing`, `ActualClosing`, `Variance`, `Notes`, `ShiftId` | The closest thing to a Z report today. |
| **HoldSales / HoldSaleItems** | Parked carts | `HoldSaleItem.ProductId` is **int** and `Quantity` is **int** (inconsistent with SaleItems). |
| **Users** | `Id`, `Username` (unique), `PasswordHash` (PBKDF2, legacy plain text upgraded on login), `RoleId` FK, `IsActive`, `LastLoginDate` | |
| **Roles / Permissions / RolePermissions** | DB-driven RBAC (see §8) | `Role.Name` unique, `Permission.Name` unique. |
| **ApplicationSettings** | `Key` (unique) / `Value` / `Description` | Generic key-value store with `SettingsRepository`. Almost unused: only 2 seeded keys plus `Setup.Completed`. |
| Other | `Expenses`, `ExpenseCategories`, `Pharmacies`, `Doctors`, `MedicalReps`, `CallSchedules`, `Employees`, `SalarySlips`, `UserFavorites`, `SyncLogs` | `Pharmacies`/`Doctors`/`MedicalReps` belong to the **PK pharma-distribution** workflow (see §8). |

## 4. Currency / number / date formatting today

A good foundation already exists. **`POSApp.UI/Helpers/RegionSettings.cs`** holds the static `Region` class plus `RegionSettingsData`:
- Fields: `CurrencySymbol`, `CurrencyName`, `NationalIdLabel`, `StatutoryDeductionLabel`, `NumberWords` (Lakh/Crore vs Million), `SymbolSide`, `DecimalPlaces`, `NumberFormat` (4 separator styles), `Dates` (DayFirst / MonthFirst / ISO).
- API: `Region.Money/MoneyWhole/MoneySigned/Number/Date/LongDate/DateTimeText/AmountInWords`, plus `x:Static` label properties for XAML. `MoneyConverter` and `DateConverter` route XAML bindings through it.
- Defaults reproduce Pakistan exactly (`Rs.`, Rupees, CNIC, EOBI, SouthAsian, DayFirst). The Store first-run wizard defaults new installs to **US Dollar** (`FirstRunSetupWindow.xaml.cs:16-25`) but still day-first dates.
- **Gaps:** there is no `Region` code (PK/US) or `Culture`. Phone formatting doesn't exist. The class lives in **UI**, so Infrastructure and Core can't use it, and the static mutable state is the cause of the flaky test.

### Hard-coded assumptions that bypass `Region` (file:line)

**Currency literals (only 3 real ones left):**
- `Views/SaleWindow.xaml:233`, `Views/WholeSaleWindow.xaml:233`: tooltip "Fixed amount discount (PKR)".
- `Core/Entities/SaleItem.cs:14`: legacy rows store `DiscountType = "PKR"`. This is data, already handled by `SaleItemViewModel.DiscountTypeIsAmount`.
- Defaults by design: `RegionSettings.cs:69,72,75,81,84,99`. Help text only: `BusinessSettingsWindow.xaml:85,128,135,143,162,173,216,223`.

**Money formatted with the machine culture (C# `ToString("N…")`) or WPF's en-US default (XAML `StringFormat`) instead of `Region`.** These ignore the shop's separator and decimal settings, and the **`N0` ones drop cents**, so `$3.49` prints as `3`:
- `ViewModels/SaleViewModel.cs:883,884,886` (**receipt price, cost and discount columns, `N0`**), `887,927,930,931,933,934,936` (`N2`), `1070` (`{…:N0}%`).
- `ViewModels/SaleReturnViewModel.cs:388,389` (`N0`), `390,419` (`N2`).
- `ViewModels/SalesReportViewModel.cs:567` (`N0`), `568,590-593,647,669-672` (`N2`).
- `ViewModels/PharmacySaleViewModel.cs:910` (`N1`), `913,914,1159,1160,1183` (`N2`).
- `ViewModels/ExpenseViewModel.cs:534`, `Views/DemandOrderDialog.xaml.cs:31`.
- XAML: `DailySummaryWindow.xaml:50,59,70,88,115-117`, `CustomerLedgerWindow.xaml:66,103,147,219`, `DashboardView.xaml:62`, `ExpenseWindow.xaml:126`, `PurchaseEntryWindow.xaml:235,237,239`, `PurchaseReturnWindow.xaml:97,99`, `SaleReturnWindow.xaml:103,105,107`, `SalesReportWindow.xaml:180,181,276,278`, `SaleWindow.xaml:165,277`, `WholeSaleWindow.xaml:165,277`, `PharmacySaleWindow.xaml:316,339`, `ShiftWindow.xaml:83-86` (`N0`), `DemandOrderDialog.xaml:59`, `ProductManagementWindow.xaml:76-78,141,155,159` (`0.##`).
- `Converters/CostPriceEncryptorConverter.cs:17,22`, `PurchaseCostEncryptorConverter.cs:17,22` (`F2`). These deliberately obfuscate cost, so leave them alone.

**Dates in fixed day-first or `dd-MMM` patterns (ambiguous to US users):**
- Receipts and printouts: `SaleViewModel.cs:781`, `SaleReturnViewModel.cs:151,331`, `SalesReportViewModel.cs:222,231,240,305,537,620,644`, `PharmacySaleViewModel.cs:875,912,1086,1158`, `ExpenseViewModel.cs:192-196,554`, `CustomerLedgerViewModel.cs:590`, `DemandOrderDialog.xaml.cs:25,98`, `SalarySlipViewModel.cs:126,399,640,669`, `Core/Entities/SalarySlip.cs:26`.
- Screens: `DailySummaryWindow.xaml:70`, `UserManagementWindow.xaml:104,108`, `ProductManagementViewModel.cs:254` (`MM/yyyy`), `DashboardViewModel.cs:101`, `CallScheduleViewModel.cs:62,86`, `MainViewModel.cs:67`, `App.xaml.cs:237`, `LicenseExpiredWindow.xaml.cs:67`, `SyncAlertDialog.xaml.cs:28,102,129`, `BusinessSettingsWindow.xaml.cs:160`.
- File-name and ID stamps (`yyyyMMdd_HHmmss` and similar in `CloudBackupService.cs:132`, `DatabaseBackupService.cs:17`, `SupplierManagementViewModel.cs:260`, `SalarySlipRepository.cs:38`) are internal and fine.

**Other PK and culture assumptions (not formatting):**
- Payment types `"Cash", "Credit", "Credit Card", "Bank Transfer"` (`SaleViewModel.cs:63`). Here **"Credit" means khata**, i.e. the customer owes the shop.
- Receipt labels "Bill No", "Mobile", "Balance Amount" (meaning change due) (`SaleViewModel.cs:780-936`).
- Invoice numbers start at `11016` (`SaleRepository.cs:107,115`). Generated barcodes are a `yyMMddHHmmss` timestamp (`ProductManagementViewModel.cs:441`): 12 digits, so they look like a UPC-A but have an invalid check digit.
- `Doctor.PmdcLicenseNo`, `Pharmacy.Ntn`, salary-slip EOBI/Lakh (already relabelled via `Region`).

## 5. Tax and discount today

- **No sales tax anywhere.** The only "tax" in the codebase is payroll `IncomeTax` and the free-text `HeaderNote` line ("NTN / VAT / EIN").
- **Discounts** (`SaleViewModel.cs:507-517`, `1090-1095`):
  - Per line: `%` or flat amount (`DiscountType`).
  - Per bill: two flat amounts, `DiscountOnProducts` and `DiscountOnBill`, subtracted from the subtotal.
- **No rounding at all.** Line totals are raw `decimal` math, e.g. `UnitPrice*Qty*Pct/100`, stored as TEXT. A 3-way-split discount can store more than 2 decimals.
- `Balance = ReceiveCash - TotalBill` is the change due when positive, or the amount added to khata on credit.

## 6. Receipt and invoice printing

- Each ViewModel builds its own WPF `FlowDocument` or `FixedDocument` inline and calls `new PrintDialog().PrintDocument(...)`, which prints to the **default printer without showing a dialog**. The call sites are `SaleViewModel.cs:692`, `SaleReturnViewModel.cs:272`, `SalesReportViewModel.cs:473`, `CustomerLedgerViewModel.cs:621`, `ExpenseViewModel.cs:562`, `PharmacySaleViewModel.cs:701`, `SalarySlipViewModel.cs:274` and `DemandOrderDialog.xaml.cs:47`. There is no shared print service and no template abstraction.
- **Format choice:** the per-Windows-user toggle `UseSmallBillFormat` (`%AppData%\POSApp\settings.json`, `SettingsManager.cs:410-424`) switches between 280 px wide (~80 mm thermal) and the printer's full page (A4/Letter). **58 mm isn't supported.**
- Header and footer text comes from `ReceiptBranding.BuildHeader/BuildFooter` (store name, address, phone, header note, footer message and note). It's stored in `receipt-branding.json`, in the same folder as the region settings.
- No barcode or QR code is printed on receipts today, but ZXing is already referenced (`BarcodeService` does Code128 and QR, so no new package is needed).

## 7. Settings storage ("Business Settings")

| Store | Location | Contents | In cloud backup? |
|---|---|---|---|
| `region-settings.json` | Direct: `%ProgramData%\ShahJeePOS\`; Store/relocated: `DataDirectory` | Currency, number/date format, ID/deduction labels | **No** |
| `receipt-branding.json` | same folder | Shop identity, header and footer | **No** |
| `%AppData%\POSApp\settings.json` | per Windows user | AutoPrint, small-bill format, auto-add, show purchase price | No |
| `ApplicationSettings` table | `posapp.db` | `DefaultProfitMarginPercentage`, `LowStockAlertEnabled`, `Setup.Completed` | **Yes** |

`BusinessSettingsWindow` (Admin, gated on `Settings.System`) has four tabs: **Shop Identity, Currency, Regional, Preview**. It reads and writes `ReceiptBranding` and `Region`, and the preview temporarily applies the draft values to the static `Region`.

**Implication:** anything we put in the JSON files is **lost on a cloud restore to a new PC**. US tax rules, PharmacyMode, PSE limits and so on should live **in the DB** (new tables plus `ApplicationSettings`) so they travel with backups.

## 8. Roles, permissions, and the existing "Pharmacy" module

- **RBAC** (migration `20260607120000_AddRbac`): there are 23 permission constants in `Core/Entities/Permissions.cs`, seeded via `HasData` (`AppDbContext.cs:468-516`). At runtime the checks go through `SessionManager.HasPermission(...)` (an O(1) HashSet) and `PermissionManager`. No code compares role-name strings.
- **Seeded system roles:** `Admin` (perms 1-20), `Manager` (1-14), `Cashier` (1-5), `PharmacyUser` (4-14, 18-23). The Store first-run disables the seeded users (admin/cashier/ali/alico) and creates the owner.
- ⚠ **The existing "Pharmacy" feature is a PK pharma-*distribution* workflow, not a pharmacy front store.** It covers B2B sales *to* pharmacies (`Pharmacy` entity = customer pharmacy with LicenseNo/NTN), doctor attribution, medical-rep call scheduling, bonus units, batch/expiry printed on the distributor invoice, and HR/salary slips. **`PharmacyUser` is a distributor-ops role, not a pharmacist.** The names collide with the US request (`Pharmacy`, `Pharmacy.Sale`, `PharmacyUser`, `PharmacyMode`), so the US features need distinct names (e.g. `FrontStorePharmacy`, `Rx`, `WillCall`).
- Nothing sensitive is audit-logged today. There's no audit table.

## 9. Editions — the most important constraint

`EditionPolicy.IsInBuild` (`Core/Services/Edition.cs:52-60`):

```csharp
// Never shipped in the Store: pharmacy is a vertical with regulatory questions,
// cloud backup needs a service-account key that must not be inside a public package, ...
AppFeature.Pharmacy or AppFeature.CloudBackup or AppFeature.YearlyLicence => edition == AppEdition.Direct,
```

- The request targets **SwiftTill on the Microsoft Store**, but today **no pharmacy features and no Firebase backup ship in the Store build**. `build-msix.ps1` also refuses to package `firebase-credentials.json`.
- Hard Rule 3 ("Firebase backup must keep working") can therefore only apply to the **Direct** edition unless cloud backup is redesigned for the Store (per-tenant auth, which is out of scope here).
- Changing `EditionPolicy` (adding a US pharmacy feature, deciding Lite vs Pro) touches Store monetization, so per Hard Rule 7 that needs your decision (see Q1).

## 10. Firebase backup/restore

- `CloudBackupService` (Direct only) works like this:
  1. Takes a SQLite native backup of the **whole `posapp.db`**.
  2. GZips it, Base64-encodes it, and splits it into roughly 700 KB chunks.
  3. Stores the chunks in Firestore at `db_backups/{projectId}/snapshots/{yyyyMMdd_HHmmss}/chunks/{n}`.
  4. Writes metadata alongside: `sizeBytes`, `sha256`, `chunkCount`, **`schemaVersion` = last applied migration id**, and `appVersion`.
  5. Keeps the last 5 snapshots and backs up automatically once a day.
- **Restore:**
  1. Checks the SHA-256 and the chunk count.
  2. Applies the schema guard `SchemaIsKnownAsync` (`CloudBackupService.cs:392-398`), which **refuses backups from a newer app** and accepts older ones.
  3. Copies the current DB to `posapp.db.prerestore`.
  4. Overwrites the DB through the SQLite backup API and tells the user to restart.
- **Why old backups restore cleanly:** on the next start `Database.Migrate()` upgrades the restored older DB. A pre-migration backup will restore on the new version **as long as every new migration is additive and defaults existing rows**. Keeping migrations additive is what keeps Hard Rule 3 true.
- **Gaps:**
  - JSON settings files aren't included (§7).
  - `.prerestore` copies only the main file, not `-wal`.
  - Between a restore and the restart, the running app holds a model that may be newer than the restored file.
- The local `BackupRestoreWindow` uses the same `DatabaseBackupService` to write file copies. The legacy per-record `FirebaseSyncService` is dormant: not registered in DI.

## 11. Bugs found that matter for this work (pre-existing, verified)

| # | Issue | Evidence | Impact on US work |
|---|---|---|---|
| B1 | **Stale sale timestamp.** `SaleDate` is set once when the VM is constructed (`SaleViewModel.cs:23`) and never reset by `NewSale()` (`:607-626`). Sale windows are hidden, not closed, so every sale in a session gets the window-open time. Same in `PharmacySaleViewModel`. | The dev DB has distinct sales with identical `SaleDate` values to the 100 ns tick. | Z reports, shift cash (`SaleDate >= OpenedAt`), tax-by-period, PSE rolling windows and will-call all depend on correct timestamps. **Must fix before Phase 1 reports.** |
| B2 | **Duplicate invoice numbers.** `GetNextInvoiceNumberAsync` takes the last row by Id and `int.TryParse`s it. A return (`"R-…"`, `SaleReturnViewModel.cs:120`) makes it fall back to `"11016"`. There's no unique index either. | The dev DB has invoice numbers 11024 to 11028 each appearing twice. | "Return by receipt barcode" looks up by invoice number and can hit the wrong sale. |
| B3 | **Shift expected cash is wrong.** It's `Σ ReceiveCash` for all sales since the shift opened (`ShiftRepository.cs:48-57`): that's cash *tendered* (before change), for **every** payment type, and it ignores refunds. `DailySummaryViewModel.cs:144,159` uses `Σ TotalBill` across all tenders. | Code read. | The over/short figure is meaningless once card tenders exist. |
| B4 | **Non-atomic sale save.** The sale is inserted, then each product is updated with its own `SaveChanges`, then the customer (`SaleViewModel.cs:570-591`). A crash mid-way leaves stock or khata inconsistent. | Code read. | FEFO batch deduction, PSE log and Rx status must be in **one transaction**. |
| B5 | **`SaleItem.Product` navigation is always null.** `SaleItem.ProductId` is a string but Product's key is an int, so EF created a shadow FK `ProductId1` that's never set. As a result, `GetSalesByCategoryAsync` and `GetRecentSalesItemsAsync` resolve everything as "Uncategorized" or null product. | `AppDbContextModelSnapshot.cs:945,969`. The dev DB has 69 of 69 `ProductId1` values NULL. | The "by department" reports (2.8) must join on `ProductId` string or `Products.ProductId`, not the navigation. |
| B6 | **Stock checks and quantity rounding.** Stock is an `int`, but a decimal quantity is deducted with `Math.Ceiling` (`SaleViewModel.cs:578`). Out-of-stock is checked only on the barcode path (`:403`), not on dropdown add. | Code read. | Batch quantities need a clear unit model. |
| B7 | Barcode lookup is an exact string match (`ProductRepository.cs:29-34`). There's no UPC-A/EAN-13 normalisation, `Barcode` isn't unique, and generated codes are invalid UPC-A. | Code read. | Phase 1.6. |
| B8 | Missing indexes: `Sales.SaleDate`, `Sales.InvoiceNumber`, `Products.ProductId`, `SaleItems.ProductId`. Reports load full `Sale` + items + customer graphs into memory. | `sqlite_master`. | Phase 4 target: 200k lines. |

## 12. Risks

1. **Edition mismatch (§9):** shipping pharmacy features in the Store reverses an earlier deliberate decision, and Store users have no cloud backup. This needs a product decision before any code.
2. **Logic-in-ViewModel architecture:** tax, tenders, FEFO, PSE and Rx can't be tested or made atomic inside `SaleViewModel`. Proposal: a **pure `SaleCalculator` in Core** (fully unit-tested), a **transactional `CheckoutService`** in Infrastructure, and a **US checkout path selected by Region**. The PK path keeps its exact current math, locked by characterization tests written *first*.
3. **PK regression:** any shared code change can alter PK receipts and totals. Mitigation: golden tests on PK receipt text and totals before refactoring, and US behaviour only when `RegionCode == US`.
4. **Two semantics of "credit":** PK khata ("Credit" = the customer owes) and US **Store Credit** (the shop owes the customer) must be separate ledgers. Never reuse `Customer.CurrentBalance` for both.
5. **Legacy rows:** old sales have no tender rows, tax lines or batch links. Reports must fall back to `Sale.PaymentType`/`ReceiveCash` and treat tax as 0. Historical tax is never recomputed.
6. **Batch vs `Product.Stock`:** introducing `ProductBatch` means keeping `Product.Stock` as the maintained aggregate (every existing screen reads it) and seeding one batch per product from the existing `BatchNo`/`ExpiryDate`.
7. **Settings in JSON are outside backups (§7):** new business rules go in the DB.
8. **Sensitive data:** patient name/DOB and PSE purchaser ID will sit in a plain SQLite file. There's no encryption at rest. Access control and audit are the realistic scope, so we should word it as "designed to limit access", which matches your rule.
9. **Static `Region` cache:** moving formatting into an injectable Core service must keep the static facade working for ~50 call sites and XAML `x:Static`.
10. **Command-line flags under MSIX** (`--seed-demo-us`, `--screenshot-mode`) need an app execution alias or an unpackaged run. Easiest: seed and screenshot from a Debug, unpackaged build with `POSAPP_DATA_DIR` pointing at a demo folder (the mechanism already exists).
11. `graphify-out/` is **tracked in git** and stale (built from `dfcae2f`). I did not rebuild it, to keep Phase 0 read-only.

## 13. Proposed phase plan (adjusted)

**Phase 1a — Safety and foundation (no visible PK change)**
- Pre-migration DB backup in `App.xaml.cs` when `GetPendingMigrations()` is non-empty (`posapp.premigrate-<migration>.db`).
- Fix the 2 failing tests (isolation via `POSAPP_DATA_DIR` plus an xUnit collection for `Region` state).
- Characterization tests for current PK totals and the receipt text.
- `IFormatService` in **Core** (pure, culture-parameterised). `Region` becomes a facade over it. Add `RegionCode` (PK/US, default PK when absent) and `Culture`, plus phone formatting.
- Route every format string listed in §4 through it. PK output must stay byte-identical.
- Bug fixes B1, B2, B5 (see Q3), plus indexes from B8.

**Phase 1b — US checkout core**
- `TaxRate`, `TaxCategory`, store tax config (DB). `Product.TaxCategoryId` (default General).
- Customer tax-exempt flag and certificate number.
- `SaleItem` tax columns and a `SaleTaxLine` (per-rate totals) table.
- `SalePayment` (tender) table: split tender, card brand / last-4 / auth only. `IPaymentProvider` plus a no-op implementation.
- Pure `SaleCalculator` with `AwayFromZero` rounding at line-tax level. Transactional `CheckoutService`. US checkout path.

**Phase 1c — Drawer, receipts, identifiers**
- Shift tender totals, X/Z reports (80 mm and A4), US receipt template (PK template untouched).
- UPC-A/UPC-E/EAN-13 validation and cross-matching, NDC normalisation, product fields (Manufacturer/Strength/DosageForm/PackSize).
- Valid in-store `2`-prefix barcode generation for US.

**Phase 2 — Pharmacy front store**, as briefed, with these adjustments:
- Gate on a **new** `AppFeature` (name TBD, see Q1), not the existing distributor `Pharmacy` feature.
- `ProductBatch` seeded from existing `BatchNo`/`ExpiryDate`.
- New `AuditLog` table.
- Roles added additively (see Q4).
- Reports join on product code (B5).

**Phase 3 — Demo data:** `DemoDataSeeder` in Infrastructure, writing to a separate data dir via `POSAPP_DATA_DIR`, with a fixed seed. It bypasses `SyncLogInterceptor` and uses bulk inserts. Screenshot mode runs on an unpackaged Debug build.

**Phase 4 — Hardening and packaging**, as briefed. Migration tests use a copy of `clients/*` PK DBs if you provide one, otherwise the dev `posapp.db`.

No new NuGet packages are expected for Phases 1 to 3: ZXing already covers receipt barcodes/QR, and CSV will be hand-rolled.

## 14. Open questions for Danish (blocking Phase 1 unless noted)

1. **Edition placement (blocking).** Where do the US pharmacy front-store features ship? Options: (a) Store **Pro** only, (b) Store Lite and Pro, (c) Direct only. My recommendation is (a), under a new `AppFeature.FrontStorePharmacy`. The existing distributor `Pharmacy` feature stays Direct-only. Cloud backup stays Direct-only (no service key in a public package), so Store users rely on local backups.
2. **Settings location.** OK to put all *new* business rules (tax config, PharmacyMode, PSE limits, will-call aging days) in the DB, where backups cover them, while keeping display formatting in `region-settings.json`?
3. **PK-visible bug fixes.** B1 (stale sale time), B2 (duplicate invoice numbers) and B3 (shift expected cash) are wrong for the PK customer too. Fix globally (my recommendation for B1/B2), or only on the US path? B3 changes the PK over/short number.
4. **Role mapping.** Proposal: add `Owner`, `Pharmacist`, `PharmacyTech` as new system roles. Keep `Admin` as is (Owner-equivalent permissions, no rename), and keep `Manager`/`Cashier`. **Do not** map the existing `PharmacyUser` to Pharmacist, because it's the distributor role. OK?
5. **Real PK database for migration testing (non-blocking).** Can I use a copy of a client DB from `clients/` (read-only copy into a temp folder), or will you supply one?
6. **Branch.** I committed this report on a new branch `feature/us-pharmacy-expansion` cut from `feature/swifttill-store-edition`. Keep that, or would you prefer the work on the store-edition branch?
