# Astra Desk

**🌐 Website & download: [astra-desk.xyz](https://www.astra-desk.xyz)**

Make Windows 11 yours: a transparent custom taskbar, animated (anime) wallpapers with effects, a desktop clock, an animated
screensaver, and one-click window layouts, in a futuristic settings app.

## Install
Run **`dist\AstraDesk-Setup.exe`** and click **Install**. It installs for your account only (no admin needed): Start menu
entry (search "Astra Desk"), optional desktop shortcut, and an entry in **Settings > Apps** to uninstall. Nothing else to install: Windows 11 already has the
WebView2 runtime it uses.

## Open it
Start menu → **Astra Desk**, the ⚙ button on the custom bar, right-click the custom bar, or **Ctrl + Alt + S**.

## What it does
| Page | Highlights |
|---|---|
| Taskbar | Windows bar, auto-hide, or a **custom bar**; Windows taskbar items and tray icons |
| Custom bar | Fully transparent background, drag elements into left / center / right, pinned apps, one bar per screen |
| Workshop | **Wallhaven** anime search (no key), **MoeWalls / MotionBgs** animated clips (download installs them), Pexels / Pixabay (free keys) |
| Wallpaper | Videos, GIFs or images; Motion + Overlay effects (sakura, rain, glitch…); playlist; pause during games / on battery; screensaver; lock screen; desktop clock (5 styles) |
| Window layout | Tile all windows: columns, grid, focus |
| Desktop | Desktop icons, dark mode, start with Windows, keyboard shortcuts |

## Keyboard shortcuts
Ctrl+Alt+W next wallpaper · Ctrl+Alt+P pause/resume · Ctrl+Alt+D desktop icons · Ctrl+Alt+B custom bar · Ctrl+Alt+T tile
windows · Ctrl+Alt+S open settings

## Quit or uninstall
**⏻ Quit** in the settings sidebar (or `DesktopStyler.exe --quit`) brings the normal Windows taskbar and wallpaper back.
Uninstall from **Settings > Apps > Astra Desk**: it restores the Windows taskbar, desktop icons, startup and
screensaver settings. Wallpapers you downloaded stay in `Videos\Wallpapers`.

## Good to know
- The Windows lock screen can only show still pictures, so it gets a snapshot; the animated version is the screensaver.
- Power friendly: the wallpaper pauses during games and full-screen apps, on battery (optional), and when you're away;
  the screensaver and "turn off my screen" happen on time even with a video wallpaper.
- Anime art belongs to its creators and studios: keep downloads on your own desktop, don't redistribute them.
- Settings and logs: `%LOCALAPPDATA%\DesktopStyler`.

## Build from source
`dotnet build -c Release` (needs the .NET 10 SDK). `tools\publish.ps1` makes `dist\DesktopStyler.exe` and the installer `dist\AstraDesk-Setup.exe` (needs Inno Setup 6: `winget install JRSoftware.InnoSetup`);
`tools\make-icon.ps1` regenerates the icon. Tests: see `TESTING.md`.
