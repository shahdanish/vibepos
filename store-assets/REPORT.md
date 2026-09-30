# SwiftTill: Microsoft Store listing assets report

Generated 2026-09-30 from branch `feature/swifttill-store-edition`. Store build audited =
MSIX edition (`StoreLite`/`StorePro`); screenshots taken as **Store Pro**.

## 1. Asset checklist → Partner Center fields

| Partner Center field (Store listing → Store logos / Screenshots / …) | File | Status |
|---|---|---|
| Store logos → **9:16 Poster art** 1440×2160 (main logo on Win 10/11) | `logos/poster-9x16-1440x2160.png` | ✅ |
| Store logos → 9:16 Poster art 720×1080 | `logos/poster-9x16-720x1080.png` | ✅ |
| Store logos → **1:1 Box art** 2160×2160 | `logos/boxart-1x1-2160x2160.png` | ✅ |
| Store logos → 1:1 Box art 1080×1080 | `logos/boxart-1x1-1080x1080.png` | ✅ |
| Store logos → **1:1 App tile icon** 300×300 | `logos/tile-300.png` | ✅ |
| Store logos → 1:1 App tile icon 150×150 | `logos/tile-150.png` | ✅ |
| Store logos → 1:1 App tile icon 71×71 (simplified mark) | `logos/tile-71.png` | ✅ |
| Promotional images → **16:9 Super hero art** 3840×2160 (no text; bottom quarter + bottom-right kept clear) | `hero/superhero-16x9-3840x2160.png` | ✅ |
| Promotional images → 16:9 Super hero art 1920×1080 | `hero/superhero-16x9-1920x1080.png` | ✅ |
| Screenshots → **Desktop** (real app, demo data), in upload order | `screenshots/01-billing.png` … `07-backup.png` | ✅ 7/7 |
| Screenshots → Desktop, framed alternative (caption ≤ 6 words, capture pasted 1:1) | `screenshots/framed/*.png` (2560×1440) | ✅ 7/7 (optional; upload **either** set) |
| Product features (up to 20) | `listing.md` → 12 bullets | ✅ |
| Search terms (7, ≤ 21 words) | `listing.md` → 7 keywords, 14 words | ✅ |
| Short title | `listing.md` → not needed (optional: `SwiftTill POS`) | ✅ |
| What's new in this version | blank (first submission) | ✅ |
| Copyright and trademark info | `© 2026 SwiftTill. All rights reserved.` | ✅ |
| Description / Short description | already written by you; **not in brief or repo**, see §4 | ⚠ needs edits |
| Xbox: branded key art, titled hero art, promotional square art | none | ⏭ skipped (§5) |
| Trailers → **Trailer** (MP4, H.264 High 1080p30, AAC silent track, 39.3 s, 3.0 MB) | `trailer/swifttill-trailer-1080p.mp4` | ✅ |
| Trailers → **Trailer thumbnail** 1920×1080 PNG (< 2 MB) | `trailer/trailer-thumbnail-1920x1080.png` (743 KB) | ✅ |

Screenshots (upload order):

| # | File | Screen | Shows |
|---|---|---|---|
| 1 | `01-billing.png` | Sales → Sale | 6 lines scanned by barcode, total Rs. 3,630, cash 4,000 → change 370 |
| 2 | `02-receipt.png` | Admin → Business Settings → Preview | receipt as it prints: Demo Mart header, amount in words, footer |
| 3 | `03-inventory.png` | Main overview | top sellers (30 days) + red low-stock list (4 items) |
| 4 | `04-sales-report.png` | Reports → Sales Report → This month | 369 sales, revenue / cost / profit / discount totals |
| 5 | `05-customers.png` | Finance → Khata | customer balances, payment history, record payment |
| 6 | `06-users.png` | Admin → Users | Admin + Cashier accounts |
| 7 | `07-backup.png` | Admin → Backup | local backup folder, 3 backups, one made by the app during capture |

## 2. Verification (`verify.ps1`)

```
File                                   Expected  Actual    SizeMB Limit PNG Result
----                                   --------  ------    ------ ----- --- ------
logos/poster-9x16-1440x2160.png        1440x2160 1440x2160 1.02   50 MB yes PASS
logos/poster-9x16-720x1080.png         720x1080  720x1080  0.31   50 MB yes PASS
logos/boxart-1x1-2160x2160.png         2160x2160 2160x2160 1.41   50 MB yes PASS
logos/boxart-1x1-1080x1080.png         1080x1080 1080x1080 0.44   50 MB yes PASS
logos/tile-300.png                     300x300   300x300   0.02   5 MB  yes PASS
logos/tile-150.png                     150x150   150x150   0.01   5 MB  yes PASS
logos/tile-71.png                      71x71     71x71     0.00   5 MB  yes PASS
hero/superhero-16x9-3840x2160.png      3840x2160 3840x2160 2.94   50 MB yes PASS
hero/superhero-16x9-1920x1080.png      1920x1080 1920x1080 0.90   50 MB yes PASS
screenshots/01-billing.png             1920x1080 1920x1080 0.11   50 MB yes PASS
screenshots/02-receipt.png             1920x1080 1920x1080 0.21   50 MB yes PASS
screenshots/03-inventory.png           1920x1080 1920x1080 0.09   50 MB yes PASS
screenshots/04-sales-report.png        1920x1080 1920x1080 0.12   50 MB yes PASS
screenshots/05-customers.png           1920x1080 1920x1080 0.12   50 MB yes PASS
screenshots/06-users.png               1920x1080 1920x1080 0.06   50 MB yes PASS
screenshots/07-backup.png              1920x1080 1920x1080 0.06   50 MB yes PASS
screenshots/framed/01-billing.png      2560x1440 2560x1440 0.15   50 MB yes PASS
screenshots/framed/02-receipt.png      2560x1440 2560x1440 0.18   50 MB yes PASS
screenshots/framed/03-inventory.png    2560x1440 2560x1440 0.12   50 MB yes PASS
screenshots/framed/04-sales-report.png 2560x1440 2560x1440 0.17   50 MB yes PASS
screenshots/framed/05-customers.png    2560x1440 2560x1440 0.18   50 MB yes PASS
screenshots/framed/06-users.png        2560x1440 2560x1440 0.09   50 MB yes PASS
screenshots/framed/07-backup.png       2560x1440 2560x1440 0.09   50 MB yes PASS
trailer/trailer-thumbnail-1920x1080.png 1920x1080 1920x1080 0.73   2 MB  yes PASS

24 checked, 24 passed, 0 failed
trailer/swifttill-trailer-1080p.mp4  3.02 MB  limit 2 GB  PASS
```

## 3. How it was made

- **Mark:** vector redraw of the existing `POSApp.UI/app-icon.png` (`src/logo.svg`) plus a
  simplified ≤71px variant (`src/logo-simple.svg`); palette/fonts in `src/brand.json`. No new logo.
- **Images:** `python src/render.py`. HTML/SVG pages rendered by Edge headless at device scale 1;
  tiles under 600px are rendered at 1200px and downscaled with Pillow (Edge has a ~500px
  minimum window width).
- **Demo data:** `dotnet run --project seed/DemoSeed` builds `seed/demo.sqlite` with the app's
  own EF migrations in a temp folder. "Demo Mart": 40 generic products (barcodes use the
  GS1 in-store prefix `200…`, never a real brand), 10 fictional customers with `0300-00000xx`
  placeholder numbers, 3 suppliers (`.example` emails), 2 users, 369 sales over 21 days
  (Rs. 562k), khata payments.
- **Capture:** `capture/Capture-Screenshots.ps1`. Windows UI Automation (built into Windows,
  nothing installed) drives the real app. Every window's visible frame is sized to exactly
  1920×1080 and screen-grabbed. No retouching.
- **Nothing outside `store-assets/` was modified.** The only side effects: a Debug build in
  `POSApp.UI/bin/Debug` (gitignored) and a temporary `subst B:` drive, removed at the end.
  `store-assets/.gitignore` excludes the runtime copy and generated HTML; `*.sqlite` is already
  ignored repo-wide.

### Trailer

Real screen recording of the app on the Demo Mart data (`capture/Record-Trailer.ps1`, ffmpeg
gdigrab 1920×1080, cursor hidden), cut to the marked scene spans and composed by
`src/trailer.py`. App footage is only trimmed, time-scaled (billing 1.3×, report 1.1×) and
scaled to 1600×900 on the brand background. Nothing inside the app window is edited.

| Time | Content | Caption |
|---|---|---|
| 0:00 | Intro card: mark, SwiftTill, "Point of sale for small shops on Windows" | none |
| 0:03 | Sale: 7 barcode scans, cash 4,000, change Rs. 370 | Scan, bill and give change in seconds |
| 0:15 | Business Settings → receipt Preview | Your shop's name on every receipt |
| 0:19 | Overview: top sellers + low stock | See what needs restocking, instantly |
| 0:22 | Sales report: this week → this month → invoice detail | Sales and profit at a glance |
| 0:29 | Khata: two customers' balances and payments | Customer credit (khata) made simple |
| 0:35 | Outro card: features, "Works offline · Available on the Microsoft Store" | none |

Silent by choice: no music, so no licensing risk. A silent AAC track is included for player
compatibility. ffmpeg came from the `imageio-ffmpeg` pip package, installed into
`store-assets/tools/` (gitignored), not system-wide.

## 4. Feature audit summary (details in `listing.md`)

| Claim | Verdict |
|---|---|
| Windows 10/11, offline-first local SQLite, grocery/general retail, barcode + keyboard billing, receipts, inventory, customers, sales reports | ✅ present in Store build |
| Suppliers, custom roles / more than 2 users, wholesale, Excel export | ✅ present, **Pro add-on only** (label as Pro) |
| **Pharmacies** | ❌ not in Store build (`EditionPolicy.IsInBuild(Pharmacy)` = Direct only) |
| **Cloud backup/restore (Firebase)** | ❌ not in Store build (`IsInBuild(CloudBackup)` = Direct only); use "local backup & restore" |
| **Custom invoice templates** | ❌ no template feature; only receipt header/footer text + 80mm/A4 |
| **Stock reports** | ❌ no stock report; only low-stock alerts + Demand Order |

**Action:** remove or reword those four claims in the existing description, short description and
4 features before submitting. Certification checks that listing claims match the app. The new
bullets and keywords in `listing.md` use none of them. The existing text wasn't in the brief or
the repo, so paste it for a sentence-by-sentence pass.

## 5. Skipped

| Item | Reason |
|---|---|
| Xbox branded key art (1920×1080 / 3840×2160) | skipped: not an Xbox product |
| Xbox titled hero art | skipped: not an Xbox product |
| Xbox promotional square art | skipped: not an Xbox product |
| Screenshot "Cloud backup & restore" (brief's #7) | replaced by **local** Backup & Restore: cloud backup isn't in the Store build, so showing it would misrepresent the product |
| Real printed receipt (paper/PDF) | the only printer here is Microsoft Print to PDF, and a PDF isn't an app screen; the app's own receipt Preview is used instead |

## 6. Manual steps left for you

1. **Fix the listing copy** per §4 (pharmacy, cloud backup, invoice templates, stock reports).
2. **Name casing:** artwork says **SwiftTill** (per the brief); the app UI and window titles say
   **Swifttill**, and the Partner Center reservation is **swifttill** (`packaging/store-identity.json`).
   Pick one spelling and use it everywhere. Artwork is regenerated with `python src/render.py`
   after editing `Swift<b>Till</b>` in `src/render.py`.
3. **Optional 100 % scaling re-capture:** this PC was at 125 %, so the UI in the screenshots is
   drawn larger. Set 100 %, then follow `capture/MANUAL.md` → Re-run.
4. **Trailer upload:** Partner Center → Store listing → Trailers → add `trailer/swifttill-trailer-1080p.mp4` with `trailer/trailer-thumbnail-1920x1080.png` and a title (e.g. "SwiftTill in 40 seconds"). To add music later, mux a track you have rights to: `ffmpeg -i trailer.mp4 -i music.mp3 -map 0:v -map 1:a -c:v copy -shortest out.mp4`.
5. **Pick one screenshot set** to upload (raw `screenshots/*.png` or `screenshots/framed/*.png`),
   in the order 01 → 07.
6. **Review the mark:** `src/logo.svg` is a faithful vector redraw of the existing icon.
   Consider also swapping it in for `POSApp.UI/app-icon.png` (784×1168 raster) so the MSIX tiles
   from `build-msix.ps1` are sharp too. Not done, since source is outside `store-assets/`.

### Product issues found along the way (not fixed: outside scope)

- Store build still seeds and shows the **PharmacyUser** role (Roles screen) though pharmacy
  screens aren't in that build.
- Backup screen defaults to `Documents\POSApp_Backups`, and the path is read-only text, so it shows
  the Windows account name. Under MSIX, consider a default inside `AppPaths.DataDirectory`.
- Khata: after clicking **All**, the **Today** filter button stays highlighted (active-filter
  styling doesn't follow the selection).
- Brief says 1440×2160 is "9:16"; it's actually **2:3**. Partner Center's own poster spec is
  720×1080 / 1440×2160, so the files are correct; only the label in the brief is off.
