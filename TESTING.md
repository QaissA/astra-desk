# Testing Astra Desk

Run all of this after every change (from the project folder). Each script restores your own settings at the end.

```powershell
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File tests\smoke.ps1              # add -RestartExplorer for the Explorer check
bin\Release\net10.0-windows10.0.19041.0\DesktopStyler.exe --uitest <folder> [--download]
powershell -ExecutionPolicy Bypass -File tools\publish.ps1            # builds dist\DesktopStyler.exe
powershell -ExecutionPolicy Bypass -File tests\install-test.ps1
```

## 1. Unit checks: `DesktopStyler.exe --selftest` (69 checks, run by smoke.ps1)
Result in the exit code and `%LOCALAPPDATA%\DesktopStyler\selftest.txt`.
Window tiling math; custom-bar zone rules and center overlap; settings loading/migration/corrupt files; Workshop parsing
(Pexels, Pixabay, Wallhaven), file-type safety, download allow-list; site-browser look-alike domains and media-only installs;
wallpaper page (motion, overlays incl. glitch, speed, culture-safe CSS, pause hooks); clock (styles, positions, sizes, 12/24 h,
locale); pause rules (full screen, locked, battery, maximized, manual, away); idle actions (screensaver / screen off at the
right time, once); playlist order/shuffle; screensaver arguments; multi-monitor split and window-to-screen mapping;
version format; registry-write validation.

## 2. Live app checks: `tests\smoke.ps1 -RestartExplorer -IdleTest` (42 checks)
Custom bar replaces the Windows taskbar and reserves space Â· re-hidden if Explorer shows it Â· real clicks: desktop icons,
Start, Search, Task view, Quick settings, Notifications open their Windows panels Â· real mouse wheel on the volume button
sends volume down/up Â· Explorer restart recovery (self-healing space reservation) Â· Normal mode / crash recovery / `--quit`
restore the Windows taskbar Â· multi-monitor via `--test-screens 2` (a bar, wallpaper and clock per screen) Â· pause while a
real full-screen window is in front Â· single instance Â· Ctrl+Alt+P / Ctrl+Alt+W Â· screensaver covers the screen and is
registered with Windows Â· idle end-to-end via `--test-idle 12` (wallpaper pauses when away, screensaver starts on time,
a mouse move ends it) Â· your settings, taskbar and screensaver restored afterwards.

## 3. UI pass: `--uitest <folder> [--download]`
Opens the settings window, visits all 6 pages (top and bottom), runs a live Wallhaven search, drags an element between
custom-bar zones and moves the transparency slider (then restores them), checks Pexels/Pixabay against their live servers
with invalid keys, counts shortcuts and zone chips, records script errors, saves screenshots + `report.txt`, then quits.
`--download` also installs a real clip through the MoeWalls window (wallpaper unchanged, file deleted afterwards).

## 4. Installer: `tests\install-test.ps1` (12 checks)
Published single-file app runs on its own Â· installs per-user with Start menu + desktop shortcuts and a Settings > Apps entry Â·
installed app runs and quits Â· uninstall removes program, shortcuts, Apps entry and startup entry, and keeps your settings.

## 5. Clean PC: `tests\sandbox\run.ps1` (Windows Sandbox)
Starts a disposable, clean Windows (no .NET, never had the app), maps `dist\` in, and runs `tests\sandbox\inside.ps1` there:
self-test, install, first-run UI pass with screenshots (welcome screen, live Wallhaven search), run, quit, uninstall with
`--purge`, and checks nothing is left behind. Needs the Windows Sandbox feature (Windows Pro, enabled once + reboot).

## 6. Verified by hand, and what's left
- [x] **"Ask for sign-in when it ends"** with the installed product: screensaver ran, a mouse move ended it, Windows locked (LogonUI)
- [x] Battery pause (charger unplugged), lock screen snapshot + sync
- [ ] **A real second monitor**: to do when one is available (the multi-monitor code is tested with `--test-screens 2`)
- Optional feature, not a gap: **Pexels / Pixabay** results appear once you add your own free API keys (requests and error
  handling are tested live with invalid keys)
- [x] Volume: smoke.ps1 reads the real speaker level (Core Audio called directly; the usual .NET cast fails on this PC's
  audio driver) and checks one wheel notch on the custom bar = one step down / up, then restores your level
- Run the live UI tests (smoke.ps1, especially `-RestartExplorer` which closes File Explorer windows) when nobody is using the PC.

