# License System — Yearly Expiry & Renewal

This folder holds the **vendor-only** tools and documentation for POSApp's yearly
time-limited license. The app runs for **one year from the first installation**, then
blocks itself and shows your contact number until the customer buys a renewal code.

> ⚠️ **Keep this folder private.** Never copy `generate-renewal-code.ps1` (or this README)
> onto a customer's machine. It contains the secret used to sign renewal codes.

---

## 1. What the customer experiences

| When | What happens |
|------|--------------|
| First install | The app quietly stamps today's date and starts a 1-year clock. Opens normally. |
| During the year | Opens normally. In the **last 15 days** it shows a "license expiring soon" popup with your number, then still opens. |
| After 1 year | Before the login screen, a **License Expired** window blocks the app. It shows your contact number, an **Activation ID**, and a box to enter a renewal code. |
| Clock rolled back | If the Windows clock is set backwards to cheat the timer, the app detects it and blocks with a "date/time problem" message. |

The app is **fully offline** — no internet is needed for any of this.

---

## 2. How renewal works (your yearly charge)

1. The customer's app expires and shows an **Activation ID**, e.g. `A1B2C3D4-01`.
   - `A1B2C3D4` is unique to that PC (derived from its hardware ID).
   - `-01` is the renewal number (2nd renewal will be `-02`, and so on).
2. Customer pays you, then **WhatsApps a screenshot of the expiry screen** to `0313-7643443`
   (the screen tells them to do this). The Activation ID is visible in the screenshot, so
   there is nothing to read out over a call and no risk of mishearing a character.
3. **You** generate a code for it (see below) and send the code back on WhatsApp.
4. They type the code into the app → license extends **one more year** → app opens.
5. Next year the number ticks up (`-02`), so a fresh code is required — **an old code
   can never be reused.**

### Generating a code

Open PowerShell in this folder and run, using the exact Activation ID the customer gives you:

```powershell
.\generate-renewal-code.ps1 -ActivationId A1B2C3D4-01
```

Output:

```
Activation ID : A1B2C3D4-01
Renewal Code  : QZSF-BZUG-RB2C-KBO2
```

Read the **Renewal Code** back to the customer. That's it.

- The code is bound to **both** that specific PC **and** that renewal number, so it only
  works on the machine that produced the Activation ID, and only once.
- Codes are case-insensitive and the dashes are optional when typed.

---

## 3. Testing the flow quickly (1-minute mode)

Instead of waiting a year, you can compress the whole cycle to **one minute**.

In `POSApp.Infrastructure/Services/LicenseService.cs`, near the top of the class:

```csharp
// >>> SET THIS BACK TO false BEFORE BUILDING FOR CUSTOMERS. <<<
private const bool TestMode = true;   // true = 1-minute expiry, false = 1-year
```

Then:

1. **Build & run** the app → first launch seeds a 1-minute license → login screen appears.
2. **Wait ~70 seconds, close, relaunch** → the **License Expired** window appears with an
   Activation ID like `A1B2C3D4-01`.
3. **Generate a code** for that ID:
   ```powershell
   .\generate-renewal-code.ps1 -ActivationId A1B2C3D4-01
   ```
4. **Type the code** into the box → **ACTIVATE** → app continues. Licensed for another minute.
5. **Repeat** — the next expiry shows `A1B2C3D4-02`, needing a new code. The old `-01` code
   is rejected (replay protection).

### Clearing the stored license (between tests / before shipping)

The license record is cached on the machine. If you switch modes (test ↔ production) and
want a clean "first install", clear it first:

```powershell
Remove-Item "HKCU:\Software\ShahJeePOS\License" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:ProgramData\ShahJeePOS\.license" -Force -ErrorAction SilentlyContinue
```

---

## 4. Before shipping to customers — checklist

Edit these in `POSApp.Infrastructure/Services/LicenseService.cs`:

- [ ] `TestMode` → **`false`** (so the license lasts a year, not a minute).
      **This is the single most important item on this list.** A build shipped with
      `TestMode = true` expires one minute after the customer's first launch — they get the
      License Expired screen within days of installing. It has happened once already, which
      is why `RepairShortPeriod()` now exists (see below).
- [ ] `RenewalWhatsAppNumber` → your **real WhatsApp number** (currently `0313-7643443`).
- [ ] `RenewalSecret` → your **own private secret**, set **once**. Then set the identical value
      in `$Secret` inside `generate-renewal-code.ps1`. **The two must match exactly.**
      Do not change it again after shipping, or previously issued codes stop working.
- [ ] Clear the stored license (command in section 3) so your build starts as a fresh install.

Then rebuild and install as usual.

---

## 5. Self-repair for short/bad expiry dates

`LicenseService.RepairShortPeriod()` runs every time the stored record is loaded. It raises
any expiry that sits **below** the entitlement the record represents:

```text
entitled expiry = install date + (renewals applied + 1) × 1 year
```

- An install poisoned by a `TestMode = true` build stores `install + 1 minute`; on the next
  launch of a corrected build it is lifted to `install + 1 year`, and the customer is let
  straight back in **without needing a renewal code**.
- A legitimate record already sits at or above that value (renewing early carries the
  remaining time forward), so the repair never shortens a license and never hands free time
  to a genuinely expired one.

Nothing needs to be run on the customer's machine — just install the corrected build over
the top.

---

## 6. How it's built (for future reference)

| Piece | Location | Role |
|-------|----------|------|
| `ILicenseService` | `POSApp.Core/Interfaces/ILicenseService.cs` | Interface + `LicenseStatus` / `LicenseState`. |
| `LicenseService` | `POSApp.Infrastructure/Services/LicenseService.cs` | The engine: install date, expiry, tamper detection, renewal. |
| `LicenseExpiredWindow` | `POSApp.UI/Views/LicenseExpiredWindow.xaml(.cs)` | The blocking expiry/renewal screen. |
| Startup gate | `POSApp.UI/App.xaml.cs` | Runs `CheckLicense()` before the login window; blocks if expired. |
| Code generator | `LicenseSystem/generate-renewal-code.ps1` | Vendor tool that produces renewal codes. |

**Where the date is stored (tamper-resistant):** the install date, expiry, last-run time and
renewal count are serialized to JSON, encrypted with Windows DPAPI (LocalMachine scope), and
written to **both**:
- Registry: `HKCU\Software\ShahJeePOS\License`
- Hidden file: `C:\ProgramData\ShahJeePOS\.license`

On every launch both copies are read and reconciled (earliest install date wins, latest
expiry/renewal wins), so deleting or editing one copy does not reset the trial — the surviving
copy restores it. A monotonic "last run" timestamp catches clock rollback.

**Security note:** because the app verifies codes offline, the signing secret ships inside the
`.exe`. This reliably stops ordinary shop users from bypassing expiry, but not a determined
reverse-engineer — that trade-off is inherent to any offline licensing scheme. If you ever need
stronger protection, an online activation check is the next step.
