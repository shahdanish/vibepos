# SwiftTill — Microsoft Store listing text

Scope: this audit is for the **Store build** (MSIX → `StoreLite` / `StorePro`), which is what
Partner Center sells. The Store build's feature set is decided by
`POSApp.Core/Services/Edition.cs` → `EditionPolicy.IsInBuild` / `ProFeatures`.

## Product features (12 bullets, each ≤ 60 chars)

| # | Feature | Chars | Backed by |
|---|---|---|---|
| 1 | Fast barcode billing; scan field clears itself | 46 | `Views/SaleWindow.xaml` (Scan Barcode field), `SaleViewModel` — shown in `01-billing.png` |
| 2 | Keyboard shortcuts to save, print and start sales | 49 | `SaleWindow.xaml` KeyBindings: Ctrl+Enter / Ctrl+P / Ctrl+N |
| 3 | Prints receipts on 80mm thermal or A4 printers | 46 | `SaleViewModel.DoPrint` (`UseSmallBillFormat` 280px ≈ 80mm, else printable A4 area) |
| 4 | Your shop name, address and footer on receipts | 46 | `Helpers/ReceiptBranding.cs`, `BusinessSettingsWindow` — `02-receipt.png` |
| 5 | Products with categories, barcodes and rack locations | 53 | `ProductManagementWindow`, `CategoryManagementWindow`, `Product.Rack` |
| 6 | Low-stock alerts and a top-sellers overview | 43 | `DashboardView.xaml` (`TopGrid`, `LowGrid`), `Product.MinStockThreshold` — `03-inventory.png` |
| 7 | Customer credit ledger (khata) with payments | 44 | `CustomerLedgerWindow`, `CustomerLedgerViewModel`, `CustomerPayment` — `05-customers.png` |
| 8 | Sales reports with revenue, cost and profit | 43 | `SalesReportWindow` / `SalesReportViewModel` — `04-sales-report.png` |
| 9 | Sale returns and cash-register shifts | 37 | `SaleReturnWindow`, `ShiftWindow` (not edition-gated) |
| 10 | Local database backup and one-click restore | 43 | `BackupRestoreWindow`, `DatabaseBackupService` — `07-backup.png` |
| 11 | Works offline; your data stays on your PC | 41 | SQLite at `AppPaths.DatabasePath` (%LocalAppData%); no server needed to sell |
| 12 | Pro: suppliers, wholesale, roles, Excel export | 46 | `EditionPolicy.ProFeatures` (Purchases, Wholesale, RoleManagement, ExcelExport) |

## Search keywords (exactly 7)

| # | Keyword | Chars | Words |
|---|---|---|---|
| 1 | point of sale | 13 | 3 |
| 2 | POS | 3 | 1 |
| 3 | billing software | 16 | 2 |
| 4 | barcode scanner | 15 | 2 |
| 5 | inventory management | 20 | 2 |
| 6 | retail shop | 11 | 2 |
| 7 | receipt printing | 16 | 2 |
| | **Total** | | **14 / 21** |

## Short title

**Not needed.** The product name ("swifttill", 9 chars) already fits in 20 characters. If you
want a descriptive short title anyway: `SwiftTill POS` (13 chars).

## What's new in this version

_(blank: first submission)_

## Copyright

© 2026 SwiftTill. All rights reserved.

---

## Feature audit: existing listing claims vs. code

The existing description and short description were **not included in the brief or found in the
repo**. The table audits every claim in the brief's product summary, which is what that copy
describes. Paste the final description text to get a sentence-by-sentence pass before
submission.

| Claim | Implemented in | Store build? | Verdict |
|---|---|---|---|
| Windows 10/11 desktop app | `packaging/AppxManifest.template.xml` MinVersion 10.0.17763.0 (Win 10 1809+) | yes | ✅ present |
| Offline-first, local SQLite database | `POSApp.Data/AppDbContext.cs` (`UseSqlite`), `AppPaths` | yes | ✅ present |
| For grocery (kirana) and general retail | Sale/Products/Khata screens (generic retail) | yes | ✅ present |
| **For pharmacies** | `PharmacySaleWindow`, `PharmacyManagementWindow`, `DoctorManagementWindow` | **no**: `EditionPolicy.IsInBuild(Pharmacy)` is Direct-only | ❌ **not in Store build**; remove from listing |
| **Cloud backup/restore (Firebase)** | `Infrastructure/Services/CloudBackupService.cs` | **no**: `IsInBuild(CloudBackup)` is Direct-only (key can't ship in MSIX) | ❌ **not in Store build**; say "local backup & restore" instead |
| Barcode scanner billing | `SaleWindow` scan field + Enter, auto-clears | yes | ✅ present |
| Keyboard billing | `SaleWindow` shortcuts (Ctrl+N/Enter/P/W/Q/M, Esc) | yes | ✅ present |
| Receipts | `SaleViewModel.CreateProfessionalInvoice` + `DoPrint`, `ReceiptBranding` | yes | ✅ present |
| **Custom invoice templates** | none: only branding (header/footer text) + 80mm/A4 layout | n/a | ❌ **missing**; reword to "your shop's name and message on receipts" |
| Inventory / stock | `ProductManagementWindow`, stock decrement on sale, low-stock list | yes | ✅ present |
| Customers | `CustomerLedgerWindow` (khata) | yes | ✅ present |
| Suppliers | `SupplierManagementWindow`, `PurchaseEntryWindow` | Pro only (`AppFeature.Purchases`) | ✅ present; label as **Pro** |
| Role-based staff accounts | `UserManagementWindow`, `RoleManagementWindow`, RBAC tables | users: yes (Lite max 2); custom roles: **Pro** | ✅ present; say "Pro" for roles / >2 users |
| Example role "PharmacyUser" | seeded `Role` Id 4 | shows in Store build, but its screens don't exist there | ⚠ don't mention in listing (see REPORT → product issues) |
| Sales reports | `SalesReportWindow` | yes | ✅ present |
| **Stock reports** | no stock/valuation report; only the overview low-stock list + Demand Order | n/a | ❌ **missing**; say "low-stock alerts" instead |

**Rule applied:** none of the ❌ claims appear in the feature bullets or keywords above.
