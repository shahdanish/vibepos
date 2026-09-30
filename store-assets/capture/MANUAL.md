# Screenshot capture: how to re-run, and manual fallbacks

All 7 target screens were reached by automation (`Capture-Screenshots.ps1`); none needed manual
steps. Use this if you re-capture, or if a future UI change breaks a step.

## Re-run (about 2 minutes)

```powershell
dotnet run --project store-assets/seed/DemoSeed          # rebuild demo.sqlite with dates relative to today
powershell -ExecutionPolicy Bypass -File store-assets/capture/Capture-Screenshots.ps1
python store-assets/src/frame.py                         # optional framed set
powershell -ExecutionPolicy Bypass -File store-assets/verify.ps1
```

Don't touch the mouse or keyboard while it runs: captures are screen grabs of the app window.

## Trailer (about 3 minutes)

```powershell
python -m pip install --target store-assets/tools imageio-ffmpeg   # once; local ffmpeg
dotnet run --project store-assets/seed/DemoSeed
powershell -ExecutionPolicy Bypass -File store-assets/capture/Record-Trailer.ps1
python store-assets/src/trailer.py
```

Captions, speeds and trims live in `SCENES` in `src/trailer.py`; recording pace (typing speed,
pauses) lives in `Record-Trailer.ps1`. Keep the desktop still while recording: ffmpeg grabs the
screen region at top-left, where every app window is placed.

## How the demo DB is isolated

`Start-DemoApp.ps1` sets two env vars **for the launched process only**; no file in the app is edited:

| Variable | Value | Read by |
|---|---|---|
| `POSAPP_DATA_DIR` | `store-assets\capture\runtime` (fresh copy of `seed\demo.sqlite` → `posapp.db`, plus `seed\demo-settings\receipt-branding.json`) | `POSApp.Core/Services/AppPaths.cs` |
| `POSAPP_EDITION` | `pro` (Debug builds only) | `POSApp.Infrastructure/Services/EditionService.cs` |

The script refuses to run if the runtime folder resolves to `%LocalAppData%\ShahJeePOS` (the
real data folder). Logins: `admin` / `demo1234`, `cashier` / `demo1234`.

## Display scaling

The capture machine was at **125 %** scaling (2560×1440). The scripts size each window so its
visible frame is exactly 1920×1080 physical pixels, so the files are correct. At 125 % the UI
is drawn 25 % larger than at 100 %, so less of each list fits. For the 100 % look the brief
asked for: Settings → Display → Scale = 100 %, sign out and in, then re-run the commands above.

## Manual steps per screen (fallback)

1. **01-billing**: Sales → Sale. In *Scan Barcode* type each code + Enter: `2001000000173`,
   `2001000000203`, `2001000000258`, `2001000000265` (twice), `2001000000012`, `2001000000142`.
   Cash received `4000`. Click back into Scan Barcode. Capture.
2. **02-receipt**: Admin → Business Settings → *Preview* tab. Capture.
3. **03-inventory**: main window after login (Overview: Top sellers + Low stock). Capture.
4. **04-sales-report**: Reports → Sales Report → *This month*. Capture.
5. **05-customers**: Finance → Khata → click the first customer (Hira Malik) → *All*. Capture.
6. **06-users**: Admin → Users. Capture.
7. **07-backup**: `subst B: store-assets\capture\runtime\subst` (after the script has created
   `Demo Mart Backups` there), Admin → Backup → Browse… → `B:\Demo Mart Backups` → Select
   Folder → Create Backup → OK. Capture. Then `subst B: /d`.
   *Why:* the default backup folder is `Documents\POSApp_Backups`, which would show the Windows
   account name in the screenshot.

Capture with Win+Shift+S → Window mode only if the window is exactly 1920×1080; otherwise use
the script's `Set-WindowFrame` + `Save-WindowShot` from `UiaHelpers.ps1`.
