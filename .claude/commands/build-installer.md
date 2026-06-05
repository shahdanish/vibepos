---
description: Build a fresh dist\ShahJeePOS-Setup.exe (publish + Inno Setup compile)
allowed-tools: Bash(*), PowerShell(*)
---

Build the Shah Jee POS installer by running the project's build script.

If the user supplied a version number in `$ARGUMENTS` (e.g. "3.1"), pass it so the
installer version is bumped; otherwise keep the current version.

Run this command from the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1 $ARGUMENTS
```

When it finishes, report the output path and the version that was built. If it
fails, show the error and the most likely cause (e.g. ISCC.exe not installed, or
a compile error in the app).
