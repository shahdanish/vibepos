# SwiftTill Store discoverability update

Prepared for Microsoft Store product `9P8C848BZ5Z3`, English (United States), on 2026-10-03.

Status: saved in Partner Center and submitted as Submission 3 (`1152921505702035814`). The overview confirms **Update in certification**, with pre-processing in progress and automatic publication after approval. The existing package and pricing are unchanged. Proof: `store-update-certification-2026-10-03.png`.

## Short description

Keep your shop moving with offline POS and billing software for Windows. Scan barcodes, print receipts, manage inventory, track customer credit (khata), and review sales and profit. Start with free Lite; optional Pro subscriptions unlock more tools.

## Description

SwiftTill is offline point of sale (POS), billing, and inventory management software for Windows 10 and Windows 11. Built for grocery stores, kirana shops, convenience stores, and general retail businesses, it brings checkout, receipt printing, stock tracking, and customer credit into one desktop app.

KEEP YOUR COUNTER MOVING
Scan product barcodes or find items with the keyboard, create sales, and print receipts. Use an 80mm thermal receipt printer or an A4 printer, and add your shop name, address, and footer message to your receipts. Handle sale returns and cash-register shifts from the same app.

KNOW YOUR STOCK AND SALES
Organize products by category, barcode, and rack location. Check low-stock alerts and top-selling products to plan restocking. Review sales reports with revenue, cost, profit, and discounts so you can understand your shop's performance.

TRACK CUSTOMER CREDIT
Keep customer records, view outstanding balances, and record payments in the customer credit ledger, also known as khata. See payment history without maintaining a separate paper ledger.

WORK OFFLINE WITH LOCAL DATA
Everyday billing works without a continuous internet connection. Your sales, inventory, and customer records are stored locally on your PC. Create local database backups and restore them when needed. Back up your data before uninstalling or moving to another PC.

START WITH FREE LITE
SwiftTill Lite includes retail billing, inventory management, receipt printing, customer credit, sales reports, returns, shifts, and local backup and restore. Lite supports an owner and one cashier, with up to two active users.

EXPAND WITH OPTIONAL PRO
A paid Pro subscription unlocks wholesale billing, purchases and supplier management, expense tracking, Excel export, more than two active users, custom roles and permissions, and employee and salary-slip tools. Pro is optional and requires an in-app subscription through Microsoft Store. Internet access is needed for installation, subscription purchases, and Store license checks; everyday billing can work offline.

Designed for Windows desktop PCs. Barcode scanning uses a compatible scanner that enters barcodes as keyboard input. Receipt printing requires a compatible printer and its Windows driver. SwiftTill does not process card payments; use your payment terminal separately.

## Product features

1. Offline point of sale (POS) for Windows 10 and 11
2. Barcode billing and keyboard checkout shortcuts
3. Receipt printing for 80mm thermal and A4 printers
4. Shop name, address and footer on printed receipts
5. Inventory with categories, barcodes and rack locations
6. Low-stock alerts and top-selling product overview
7. Customer credit ledger (khata) and payment history
8. Sales reports with revenue, cost, profit and discounts
9. Sale returns and cash-register shift management
10. Local database backup and restore
11. Free Lite with up to two active staff accounts
12. Pro subscription: suppliers, wholesale and expenses
13. Pro subscription: Excel export, custom roles and HR

## Keywords

- point of sale
- POS software
- billing software
- inventory management
- barcode scanner
- receipt printing
- grocery store

Seven terms, 15 words total; each term is under 40 characters.

## Audit notes

- Partner Center shows live package `Swifttill-1.0.6.0.msix`, x64, Windows Desktop.
- Verified public audience, discoverable in Store, all worldwide markets (240), future markets enabled, base app free.
- The previous description claimed pharmacy modules, cloud backups, custom invoice templates and stock reports. These are absent from the audited Store feature set or overstated; the replacement describes local backups, receipt branding and low-stock alerts.
- Pro features checked against `POSApp.Core/Services/Edition.cs`. Recent uncommitted gift-card and loyalty work is not advertised as a released feature.
- This is a metadata update; no new application release is claimed in the What's new field.
- Product name remains the reserved `swifttill`. Use the description and keywords for search relevance rather than adding keywords to the product name.
- Verified primary category Business / Inventory + logistics. No product website URL is configured in Partner Center.
- The existing privacy text still describes pharmacy businesses and says uninstalling does not delete the database. Store package storage can be removed on uninstall, as documented in `packaging/README.md`; review that policy separately. The privacy policy was not changed as part of this search metadata update.

## Discovery beyond Microsoft Store

Store keywords improve internal Store matching; Partner Center does not expose a website's HTML metadata, canonical URLs, structured data or sitemap. For broader web discovery, create or improve a public product website with a clear POS page title, useful original content, screenshots, requirements, Lite/Pro comparison, support and privacy pages, and a direct Store download link. Add the site to Google Search Console and Bing Webmaster Tools when a domain is available. Publish tutorials for real shop workflows and collect authentic customer feedback. Search ranking and impressions are not guaranteed.

## References

- [Microsoft Store listing guidance](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [MSIX search keyword limits](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-additional-information)
- [Store visibility options](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/visibility-options)
