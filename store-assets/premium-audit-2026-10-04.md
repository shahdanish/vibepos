# Premium enablement audit — 2026-10-04

## Confirmed blocker and action

Partner Center for parent app `9P8C848BZ5Z3` listed one add-on: `pro_monthly`, subscription Store ID `9PFD6MNK2886`, status **Not submitted**. There was no yearly add-on. The existing monthly draft had all submission sections complete.

Submitted the existing monthly draft (`1152921505702009271`) with its configured settings unchanged. The certification page now confirms pre-processing and automatic publication after certification. Proof: [submission screenshot](pro-monthly-submitted-2026-10-04.png).

Existing configuration:
- Monthly billing and one-month free trial.
- USD 1.99 base price; submission overview displays PKR 209.00.
- All worldwide markets, future markets enabled, public audience.
- Can be displayed in the parent product listing; acquisition enabled.
- Release as soon as possible, no scheduled stop acquisition.

Partner Center says certification normally takes hours but may take three business days, and customer visibility may take another 24 hours after publication. Approval and a successful live purchase have not yet been verified.

## How the app enables premium

`EditionService` starts packaged MSIX copies as StoreLite. `RefreshAsync` reads Microsoft Store add-on licenses; any active license with an `InAppOfferToken` starting `pro_` grants StorePro. A later refresh returns to Lite if no such license is active. Microsoft Store license change notifications also trigger a refresh.

`MainWindow.xaml` includes the Upgrade to Pro header button, visible in Lite. `EditionGate.Require` opens the same upgrade dialog when a locked feature is requested. `UpgradeWindow` queries Store plans and calls the Store purchase UI. Plans must have a `pro_` token and a paid subscription SKU; subscription products are queried as Durable. Actual Store IDs returned by the catalog are used for purchase. The `RequestedProProductIds` array is logged/documented but is not passed as a filter to the associated-products query.

The current local Pro feature matrix includes wholesale, purchases/suppliers/returns/demand orders, expenses, Excel export, more than two active users, custom roles/permissions, employees/salary slips, and US pharmacy front-store tools (PSE logbook, FSA/HSA flags, expiry report). User permissions and regional conditions still apply.

Full pharmacy workflows, cloud backup, and offline yearly renewal-code licensing are Direct-only and are excluded from Store Pro. Unpackaged direct installs use the separate direct licensing flow. Debug builds can simulate Lite/Pro through `POSAPP_EDITION`; simulated plans are not evidence of a live Store product.

## Customer guidance

Microsoft subscriptions are purchased inside the app; absence of a Buy Premium button on the Store website is expected. Once the add-on is approved and published:
1. Install/update the app from Microsoft Store.
2. Sign in to Microsoft Store with the intended purchasing account and connect to the internet.
3. Open the app and select Upgrade to Pro, or request a locked Pro feature.
4. Select the monthly plan and Subscribe; Microsoft handles trial eligibility and checkout.
5. Confirm the badge changes to Pro and the desired feature opens, subject to staff permissions.

If plans remain unavailable after publication, use Retry and inspect `store-catalog.log` under the app's Logs directory. The current source records package/window status, Store errors, returned products, subscription eligibility, and accepted plan count. Do not treat changing the base app price as a premium unlock.

## Listing and validation limits

The live Pakistan Store web page still uses the old description advertising pharmacy and cloud backup. The parent application's Submission 3 is already in certification with revised Lite/Pro wording; this audit did not cancel or replace it. The published binary was not tested against a live purchase. The findings about feature gates describe the current local source, which contains pre-existing uncommitted changes.

Ran targeted existing tests with `dotnet test POSApp.Tests/POSApp.Tests.csproj --no-restore --filter "FullyQualifiedName~EditionPolicyTests|FullyQualifiedName~ProSubscriptionIdTests|FullyQualifiedName~StorePlanQueryTests" --verbosity minimal`: 54 passed, 0 failed. These validate local policy and plan handling, not Store certification, payment, or catalog propagation. No app source was changed during this audit.

## References

- [Partner Center subscription overview](https://partner.microsoft.com/en-us/dashboard/products/9PFD6MNK2886/overview)
- [Microsoft subscription implementation and unsupported direct Store sales](https://learn.microsoft.com/en-us/windows/uwp/monetize/enable-subscription-add-ons-for-your-app)
- [Microsoft add-on product queries](https://learn.microsoft.com/en-us/windows/uwp/monetize/get-product-info-for-apps-and-add-ons)
