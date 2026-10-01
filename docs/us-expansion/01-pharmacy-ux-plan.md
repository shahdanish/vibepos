# US Pharmacy & Sales-Screen UX — Expanded Plan

Written 2026-10-01 from Danish's request. It builds on `00-discovery.md` and keeps every rule from it: PK output stays frozen, migrations are additive only, US behaviour gates on `Region.IsUnitedStates`, and every feature is classified in `EditionPolicy`.

## What was asked (restated)

1. Sale screen: products marked as **favorites** show as one-click tiles that add the item to the cart.
2. Sale screen: align the "Auto print" / "Small bill" checkboxes properly and follow the design system exactly.
3. **Pharmacy users**: a Pharmacist can log in and manage medicines.
4. Match the standard of US pharmacy front-store POS systems, including the free ones.
5. Ship the pharmacy features in **Store Pro** (premium). Normal retail stays free (Lite).
6. Rename **"Khata"** to a US term.
7. Let a pharmacist run the shop end to end from this app.
8. Polish the UI. Keep the design system, but add **theme options**.

## Decisions taken (change them if you disagree)

| Topic | Decision | Why |
|---|---|---|
| "Khata" label | Region-aware. **US/other: "Charge Accounts"** (window title "Customer Charge Accounts"; this is the usual US pharmacy term, also called "house charge"). **PK keeps "Khata"**, because the live PK shop uses that word. | The PK-safety rule from Phase 1a. |
| Favorites | **Shop-wide by default.** Use the existing `UserFavorites` table, one row per starred product (see UX-1 status), plus an optional per-user list later. | Every cashier sees the same quick keys, as in US pharmacy POS systems. |
| Edition | Favorites, themes and alignment fixes go in **Lite (free)**. Pharmacist workspace, Rx will-call, PSE log, age checks, FSA/HSA flags, NDC/lot/expiry go in **Pro** as `AppFeature.FrontStorePharmacy`. | Your 2026-10-01 decision. The PK distributor `Pharmacy` feature stays Direct-only. |
| Roles | Add `Owner`, `Pharmacist` and `PharmacyTech` additively. Keep `Admin`, `Manager`, `Cashier` and `PharmacyUser` (the distributor role) unchanged. | Decision Q4 in `00-discovery.md`. |
| Themes | Move colour tokens out of `DesignSystem.xaml` into palette dictionaries swapped at runtime. Ship **Light (current warm brown)**, **Light + accent choices** (Brown, Pharmacy Teal, Clinical Blue, Forest Green) and **Dark** last, after hardcoded colours are removed from views. Store the choice per Windows user. | Styles already use `DynamicResource`, so the swap is cheap. Dark mode needs the leftover hex colours in views cleaned up first. |

## Phases (one commit each; stop after each phase and wait for "continue")

### UX-1: Sale screen quick keys, alignment, Charge Accounts, themes (Lite)
- **Favorites panel** on `SaleWindow` and `WholeSaleWindow`: a right-hand card (it collapses on small screens and has a ScrollViewer) with a `WrapPanel` of tiles. Each tile shows the name, price and stock badge, plus a warning colour when stock is low or the item is out of stock. Click adds qty 1, a second click increments. Hotkeys F1–F12 cover the first 12 tiles. A search box filters the tiles.
- **Star toggle** to mark favorites in `ProductManagementWindow` (grid column) and from a cart row's context menu. A "Manage quick keys" dialog lets you reorder tiles (add `SortOrder` to `UserFavorite`, which is an additive migration).
- New DS styles: `QuickTile`, `QuickTileBadge`, and `DsCheckBox`, a custom template (18px box, brand fill when checked, focus ring) so every checkbox in the app aligns to the same baseline.
- Rename Khata through `Region.CustomerAccountsLabel` (menu, window title, help text).
- **Appearance** tab in Business Settings: accent picker and density (Comfortable/Compact, for small LED screens), with a live preview.

**Status: done (2026-10-01).** Differences from the plan above, settled while building and live-testing:
- Quick keys are shop-wide by *product*: one tile per product in `UserFavorites`, whoever starred it (`UserId` records who). No special "shop" owner row is needed, and favourites saved by the old per-user code simply become tiles.
- The panel is a **full-height right rail** beside the whole sale screen, not only beside the cart. At 1366×768 the cart row is about 190px tall, which fits a single tile. The rail fits all twelve F-key tiles.
- The panel and its "★ Quick keys" toggle **appear only once the shop has starred a product**, so an upgraded shop that never uses them sees the sale screen unchanged.
- F1–F12 always mean the first twelve tiles, even while the filter hides some. Inside the cart grid F2 still edits a cell.
- The cart now scrolls to the line a scan, tile or F-key just added. On small screens new lines used to land out of view.
- Accents are applied by replacing the brand brushes in the application resources (`ThemeManager`). Palette dictionaries wait until Dark (UX-4) needs them. Accent and density are saved per Windows user. Closing Business Settings without saving puts the saved look back.
- `POSAPP_USER_SETTINGS_DIR` relocates the per-user settings file, for tests and scratch-data trials only.

### UX-2: Pharmacist workspace & roles (Pro)
- Seed roles Owner/Pharmacist/PharmacyTech and new permissions: `Rx.Pickup`, `Rx.Verify`, `Pse.Sell`, `Pse.Log.View`, `Medicines.Manage`, `Audit.View`.
- **Pharmacy home**, the landing screen after a pharmacist logs in. It has tiles for Will-call queue, Expiring soon (30/60/90 days), Low stock, Today's Rx pickups, PSE log and Counseling requests.
- **Medicine fields** on Product: NDC (normalised 11-digit), Manufacturer, Strength, Dosage form, Pack size, `IsRx`/`IsOtc`, schedule (CII–CV, display only), `RequiresPharmacistConsult`, `AgeRestriction` (18/21), `IsPse` with base mg per unit, and `IsFsaHsaEligible`.
- `ProductBatch` (lot + expiry + qty, FEFO deduction). `Product.Stock` stays as the aggregate.
- `AuditLog` table for Rx pickups, PSE sales, price overrides, voids and role changes.

### UX-3: US front-store checkout rules (Pro, US region)
This needs Phase 1b (tax/tenders) from `00-discovery.md` to land first, or alongside it.
- **Rx will-call pickup**: scan the bag label or search by patient and DOB, confirm DOB, collect the copay, record "counseling offered: accepted/declined", and capture an on-screen signature acknowledgement. Return-to-stock prompt after N days (setting).
- **PSE (CMEA) sale**: ID type/number capture, and blocks at 3.6 g/day and 9 g per 30 days per purchaser. Logbook report.
- **Age-restricted items**: ID check prompt with the DOB calculation, and the sale is blocked if under age.
- **FSA/HSA**: mark eligible lines, show the eligible subtotal on the receipt and tender screen.
- Tax-exempt Rx lines vs taxable OTC (tax categories from 1b).
- US receipt: Rx lines show the Rx number, not the drug name (privacy), plus the FSA subtotal and the counseling line.

### UX-4: Reports, demo data, dark theme, hardening
- Reports for expiry, PSE log, Rx pickup, FSA sales, sales by department, and Charge Account statements (monthly, printable).
- US pharmacy demo dataset (Phase 3 of `00-discovery.md`).
- Remove the remaining hardcoded colours in views, then ship the Dark palette.
- Live-test every flow with the `app-live-testing` recipe and update the screenshots in `store-assets/`.

## Parity checklist vs. common US pharmacy POS systems

Will-call/Rx pickup ✔ · signature/counseling log ✔ · PSE/CMEA log ✔ · age verification ✔ · FSA/HSA (IIAS flagging) ✔ · NDC + UPC scanning ✔ · lot/expiry FEFO ✔ · house charge accounts ✔ · split tender ✔ (1b) · X/Z reports ✔ (1c) · role-based access + audit ✔ · quick keys ✔ · themes ✔.

Out of scope: claims adjudication, e-prescribing and the pharmacy management system (dispensing). These belong to the PMS, not the front-store POS. Card-processor and signature-pad hardware come later through the `IPaymentProvider` stub.
