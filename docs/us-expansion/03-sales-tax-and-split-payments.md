# Sales Tax & Split Payments (Phase 1b)

Built 2026-10-02. US checkout now charges sales tax and takes split payments. Both are free in every edition (Direct, Store Lite, Store Pro). They only switch on for a shop whose country is the United States, and Pakistani tills keep exactly the screens, totals and receipts they had.

## Sales tax

- **Where it's set:**
  - **First-run setup** asks for the combined state + local rate when US Dollar is picked. It creates the usual categories:
    - General merchandise (default)
    - Non-prescription drugs (OTC)
    - Food & grocery
    - Prescription drugs (0%)
    - Non-taxable (0%)
  - **Admin → Business Settings → Sales Tax:** turn tax on or off, and add, rename, rate or remove categories, and choose the default category.
  - **Inventory → Products → Tax category:** each product uses its own category, or the default.
  - **Customer charge accounts:** a customer can be marked **tax-exempt**, with their certificate number.
- **Stored in the database:** the categories table and `Tax.Enabled`, so cloud and local backups carry them.
- **How it's calculated** (`SaleTaxCalculator`, pure and unit-tested):
  - A bill discount lowers the taxable amount. It is shared across the lines in proportion to their totals, and the last line takes the rounding remainder.
  - Each line is taxed at its own rate and rounded to the cent, half away from zero.
  - Tax is stored per line (`SaleItems.TaxRate`, `TaxAmount`), with the bill's total in `Sales.TaxTotal`.
  - An exempt customer pays no tax, and the sale records the certificate number (`Sales.TaxExemptNumber`).
- **Sale screen:** one line, "Subtotal … · Sales tax 8.25% …", above the Total.
- **Receipt:**
  1. Subtotal
  2. Discount
  3. Sales tax per rate, or "Tax exempt #…"
  4. TOTAL
  5. Each payment
  6. Change
  7. You saved
  8. Items
- **Returns:** the tax paid on the returned units is refunded too. The whole line's tax comes back if the whole line is returned, otherwise it's shared per unit. The return receipt shows "Sales tax refunded".

## Split payments (tenders)

- **US payment methods:** Cash, Card, Check, Charge Account. The original list (Cash, Credit, Credit Card, Bank Transfer) stays for every other country.
- **Single payment:** pick the method and Save or Print, as before.
  - Cash with nothing typed is the exact amount.
  - Cash short of the total is refused, with a pointer to Split or Charge Account.
  - Charge Account needs a customer and adds the amount to their balance.
- **Split… (Ctrl+T):** any mix of payments until nothing remains.
  - Only cash can be more than what's due; the rest is change.
  - **Card details:** card type, last 4 digits and auth code, copied from the shop's separate card terminal. **The card number itself is never entered or stored, and there is no card-processor integration yet.**
  - **Check:** check number.
  - **Charge Account:** that part goes on the customer's account.
  - If the bill changes after a split was entered, the split is cleared, so it can't be saved against the wrong total.
- **Stored** in the new `SalePayments` table, one row per payment (method, amount applied, amount handed over, card type, last 4, auth or check number). The sale's `PaymentType` reads `Split` when several payments were used.
- **Refunds** go back the way the sale was paid:
  - the sale's own payment method if there was one
  - cash if the split included cash, otherwise the first method
  - cash for sales made before payments were recorded

  A refund to a charge account lowers the customer's balance.

## Cash drawer

The expected cash at shift close and on the Daily Summary now counts only the **cash** part of US sales, minus cash refunds. Card, check and charge-account money never reaches the drawer. Sales with no payment rows (every Pakistani sale, and anything older) use exactly the figure they always did.

## Database (migration `AddSalesTaxAndPayments`, additive only)

- New tables: `TaxCategories`, `SalePayments` (cascades with its sale).
- New columns, all nullable or defaulted to 0/false:
  - `Sales.TaxTotal`, `Sales.TaxExemptNumber`
  - `SaleItems.TaxRate`, `SaleItems.TaxAmount`
  - `Products.TaxCategoryId`
  - `Customers.IsTaxExempt`, `Customers.TaxExemptNumber`
- **No foreign key** on `Products.TaxCategoryId`: SQLite would rebuild the Products table to add one. The repository clears it when a category is deleted.
- **No writes to existing rows:** the `UpdateData` EF scaffolded for the seed rows was removed.
- **Re-runs and backups:** re-runs are no-ops through the EF migration history. An old backup restores and upgrades through this migration; the test suite covers that.

## Limits to know

- The shop types in its own rates. There are no built-in state or local rate tables. Destination-based or product-specific state rules are up to the shop's categories.
- Prices are tax-exclusive (tax added on top), as is usual in the US.
- No card terminal integration and no FSA/HSA (IIAS) handling yet. "FSA/HSA card" is only a card type label.
- Shift X/Z reports with a payment-method breakdown and a tax report by period come in Phase 1c.

## Verified

- **Tests:** 188 pass, including 38 new: the calculator (rounding, discount sharing, exemption, per-rate totals), payment rules, the drawer count, tax settings, the US sale screen (tax, exemption, cash with change, split, charge account, receipt), tax refunds on returns, and the shift cash count.
- **Fresh US install, live:**
  - setup at 8.25% with sample data
  - a sale of three items: subtotal $20.47, tax $1.69 (0.62 + 0.58 + 0.49), total $22.16
  - paid by split: Cash $10.00 + Card (Visa ****4242) $12.16
  - receipt printed to PDF; the sale and both payment rows checked in the database
  - a return of one item: $7.49 + $0.62 tax = $8.11 refunded to cash
  - the Sales Tax tab
- **Pakistani copy, live:** no Split button, no tax rows, the original payment list, Rs. totals. The golden receipt tests are unchanged.
