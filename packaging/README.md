# Swifttill — Microsoft Store edition

One codebase ships two ways:

| | Direct (Inno Setup) | Microsoft Store (MSIX) |
|---|---|---|
| Product name | Shah Jee POS (shop name in titles) | **Swifttill** |
| Build | `Build-Installer.cmd` | `Build-MSIX.cmd 1.0.0.0` |
| Detected by | not packaged | running from an MSIX package |
| Licence | yearly renewal codes (`LicenseService`) | Microsoft Store |
| Features | everything | Lite (free) + **Pro subscription** |
| Pharmacy, cloud backup | yes | not included |
| First launch | seeded logins | setup wizard creates the owner account |

The edition logic lives in `POSApp.Core/Services/Edition.cs` (`EditionPolicy`) and
`POSApp.Infrastructure/Services/EditionService.cs`. Pro unlocks: wholesale, purchases &
suppliers, expenses, Excel export, more than 2 users, custom roles, HR.

## Try the Store behaviour without packaging (Debug builds only)

```powershell
$env:POSAPP_EDITION = 'lite'      # or 'pro' / 'direct'
$env:POSAPP_DATA_DIR = "$env:TEMP\posapp-store-test"   # keeps your real data untouched
dotnet run --project POSApp.UI
```

In Debug + `lite`, the upgrade dialog shows two sample plans and "Subscribe" simulates a successful purchase.

## Submitting

1. **Partner Center** (partner.microsoft.com → Apps and games) → *New product → MSIX or PWA app* → reserve the name **Swifttill**.
2. *Product management → Product identity*: copy **Package/Identity/Name**, **Publisher** (`CN=…`) and **PublisherDisplayName** into `packaging/store-identity.json`.
3. *Add-ons → New add-on → **Subscription***, one per plan, e.g. Product ID **`pro_monthly`** (billing period 1 month) and **`pro_yearly`** (1 year). Any Product ID starting with `pro_` unlocks Pro (`EditionService.ProProductIdPrefix`). For each: set the price, optionally a free trial (e.g. 1 month), a Store listing title such as "Swifttill Pro — Monthly", and submit it. Plans appear in the app's upgrade dialog with Store-formatted prices; a lapsed/cancelled subscription drops the shop back to Lite without touching data.
4. Build: `.\build-msix.ps1 -Version 1.0.0.0` → `dist\Swifttill-1.0.0.0.msix`. Every new submission needs a higher version; the last part stays `0`.
5. Run the certification kit (admin PowerShell):
   `& 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' test -appxpackagepath dist\Swifttill-1.0.0.0.msix -reportoutputpath dist\wack-report.xml`
6. Submission: upload the **unsigned** `.msix` (the Store signs it), pricing **Free**, screenshots (1366×768 or larger), description, age rating questionnaire, and a **privacy policy URL** — required because the app stores customer names and phone numbers. The app does not send that data anywhere in the Store edition (no cloud backup); say so in the policy.
7. Because it's a full-trust desktop app (`runFullTrust`), explain in *Submission options → Restricted capabilities*: "WPF point-of-sale app: needs direct thermal-printer access and a local SQLite database."

## Local test install

```powershell
.\build-msix.ps1 -Version 1.0.0.0 -Sign
# once, in an ADMIN PowerShell:
Import-Certificate -FilePath dist\Swifttill-TestSigning.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
.\build-msix.ps1 -Version 1.0.0.0 -Sign -Install
```

Uninstall with `Get-AppxPackage *Swifttill* | Remove-AppxPackage`. Note: uninstalling a packaged
app deletes its data — tell Store customers to use Admin → Backup first.

## Where data lives

`AppPaths` (`POSApp.Core/Services/AppPaths.cs`) puts the database and logs in
`%LocalAppData%\ShahJeePOS` (redirected into the package's private storage under MSIX). Older
direct installs that kept `posapp.db` next to the .exe are copied there on first start — the old
file is left in place.
