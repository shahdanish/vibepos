# US Defaults & Sample Data

Changed 2026-10-01 at Danish's request: the product is now US-first. New installs start as a US shop, and a first-run option loads a sample US pharmacy catalog for testing.

## What a new install does

1. **First-run setup** runs on every new install, direct and Store editions alike. It asks for:
   - shop name, phone and address
   - currency (US Dollar is preselected)
   - the owner's username and password
   - an optional **"Load the sample US pharmacy catalog"** box
2. **Seed logins and products removed:** setup deletes the old seeded logins (`admin`, `cashier`, `ali`, `alico`) and the old seeded products ("Glycerin 25gm", "Glue Stick"). After setup the only login is the owner's, plus the sample staff logins if samples were chosen.
3. **US region defaults:**
   - `$1,250.00`
   - 10/14/2026
   - 2:35 PM
   - Million/Billion in words
   - "ID Number"
   - "Social Security"

   `RegionSettingsData` defaults and `RegionSettingsData.UnitedStates()` are the same thing now.

## Sample data (optional, for testing)

`POSApp.Infrastructure/SampleData/UsPharmacySampleData.cs`:

- **113 over-the-counter items** in 12 categories: Pain & Fever, Cold/Cough/Flu, Allergy, Digestive, Sleep Aids, First Aid, Skin Care, Eye & Ear, Vitamins, Diabetes Care, Smoking Cessation and Home Health.
- **Names:** generic medicine names only (e.g. "Acetaminophen 500 mg Extra Strength Caplets, 100 ct", "Loratadine 10 mg Tablets, 30 ct"). There are **no brand names and no NDCs**, per hard rule 5. A test fails if a known brand name appears.
- **Prices and stock:** typical US store-brand shelf prices, with cost and wholesale price, stock, reorder level, aisle/shelf, lot number and expiry date.
- **Test cases built in:** a few items are deliberately low, out of stock or expiring soon.
- **Barcodes:** in-store UPC-A codes (number system 4, e.g. `420260003038`). That range is reserved for a store's own labels, so it never matches a real product.
- **Quick keys:** the 10 most common items are pre-set as quick keys F1–F10.
- **Customers:** 3 fictional charge-account customers. Their 555-01xx phone numbers are reserved for fiction.
- **Sample staff logins.** These passwords are published here and on the setup screen, so **delete them in Admin → Users before going live**:

  | Username | Password | Role |
  |---|---|---|
  | `sarah.mitchell` | `Mitchell#2026!` | Manager |
  | `james.parker` | `Parker#2026!` | Cashier |

## Existing installs (the live Pakistani shop) are not changed

Hard rule 1 still holds for every till that was in use before this change:

- **Install origin:** on its first start on this version, each database records `Install.Origin` in ApplicationSettings. The value is `Upgraded` if it already had migrations applied, otherwise `New`.
- **No wizard for upgraded direct installs:** an `Upgraded` direct install gets no setup wizard. Its seeded logins (e.g. `ali`) are real logins there and stay.
- **Upgraded install with no region file:** if it never saved region settings, the Pakistan preset it has always run on is written for it once (`RegionSettingsStore.KeepLegacyDefaultsIfUnset`). It is also mirrored into the database, so backups carry it.
- **Settings files from older builds:** these have no `RegionCode`. They are read on top of the **Pakistan** preset, so any property the file lacks keeps its Pakistani value, not the new US default (`RegionSettingsStore.Deserialize`).
- **Pakistan stays available:** Pakistan remains in the setup currency list and in Business Settings → Regional, now listed after the United States.

All of this was verified live on a copy of a Pakistani database with its settings removed. It went straight to login, `ali` still worked, the sale screen showed `Rs. 0.00`, and the database recorded `Install.Origin = Upgraded`.

## Not changed

- Database column names such as `Cnic` and `EobiDeduction`. Renames are forbidden by hard rule 2, and their on-screen labels follow the region.
- The Pakistani pharma-distributor module, which is Direct-only and gated by role. Its printed warranty text is client-specific.
- `store-assets/seed/DemoSeed`, the Store screenshot database.
