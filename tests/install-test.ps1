# Installer test: installs dist\DesktopStyler.exe for the current user, runs it, uninstalls it, and checks Windows is
# left clean. Your own settings, startup entry and desktop-icon state are restored afterwards; the dev copy restarts.
# Usage (after tools\publish.ps1):  powershell -ExecutionPolicy Bypass -File tests\install-test.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root 'dist\DesktopStyler.exe'
$dev = Get-ChildItem (Join-Path $root 'bin\Release') -Recurse -Filter DesktopStyler.exe | Where-Object FullName -notmatch '\\win-x64\\' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\DesktopStyler'
$installed = Join-Path $installDir 'DesktopStyler.exe'
$startLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'Astra Desk.lnk'
$deskLink = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Astra Desk.lnk'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopStyler'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$settings = Join-Path $env:LOCALAPPDATA 'DesktopStyler\settings.json'
if (-not (Test-Path $dist)) { throw "run tools\publish.ps1 first" }

Add-Type @'
using System; using System.Runtime.InteropServices;
public static class D {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(P cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  static IntPtr DefView() { IntPtr dv = IntPtr.Zero; EnumWindows((t, l) => { dv = FindWindowEx(t, IntPtr.Zero, "SHELLDLL_DefView", null); return dv == IntPtr.Zero; }, IntPtr.Zero); return dv; }
  public static bool Icons() { return IsWindowVisible(FindWindowEx(DefView(), IntPtr.Zero, "SysListView32", null)); }
  public static void SetIcons(bool on) { if (Icons() != on) SendMessage(DefView(), 0x111, (IntPtr)0x7402, IntPtr.Zero); }
}
'@
$fails = 0
function Check($ok, $name) { if ($ok) { Write-Host "PASS $name" -ForegroundColor Green } else { Write-Host "FAIL $name" -ForegroundColor Red; $script:fails++ } }

$settingsBackup = Get-Content $settings -Raw -ErrorAction SilentlyContinue
$runBackup = (Get-ItemProperty $runKey -Name DesktopStyler -ErrorAction SilentlyContinue).DesktopStyler
$iconsBackup = [D]::Icons()
try {
  "{0:N1} MB single-file app" -f ((Get-Item $dist).Length / 1MB)
  $st = Start-Process $dist -ArgumentList '--selftest' -PassThru -Wait
  Check ($st.ExitCode -eq 0) 'published app passes its self-test (runs without .NET installed separately)'

  Start-Process $dist -ArgumentList '--install', '--quiet' -Wait
  Check (Test-Path $installed) "installed to $installDir"
  Check ((Test-Path $startLink) -and (Test-Path $deskLink)) 'Start menu and desktop shortcuts created'
  $target = (New-Object -ComObject WScript.Shell).CreateShortcut($startLink).TargetPath
  Check ($target -eq $installed) 'Start menu shortcut points to the installed app'
  $entry = Get-ItemProperty $uninstallKey -ErrorAction SilentlyContinue
  Check ($entry.DisplayName -eq 'Astra Desk' -and $entry.UninstallString -like '*--uninstall*' -and $entry.DisplayVersion) "listed in Settings > Apps (v$($entry.DisplayVersion))"

  $p = Start-Process $installed -ArgumentList '--tray' -PassThru; Start-Sleep 8
  Check (-not $p.HasExited) 'installed app starts and keeps running'
  Start-Process $installed -ArgumentList '--quit' -Wait; Start-Sleep 3
  Check ($p.HasExited) 'installed app quits cleanly'

  Start-Process $installed -ArgumentList '--uninstall', '--quiet' -Wait; Start-Sleep 6 # folder removal runs a moment after
  Check (-not (Test-Path $installDir)) 'uninstall removes the program folder'
  Check (-not (Test-Path $startLink) -and -not (Test-Path $deskLink)) 'uninstall removes the shortcuts'
  Check (-not (Test-Path $uninstallKey)) 'uninstall removes the Settings > Apps entry'
  Check (-not (Get-ItemProperty $runKey -Name DesktopStyler -ErrorAction SilentlyContinue)) 'uninstall removes the startup entry'
  Check (Test-Path $settings) 'uninstall keeps your settings unless you ask to delete them'
}
finally {
  if ($settingsBackup) { $settingsBackup | Set-Content $settings -Encoding utf8 }
  if ($runBackup) { Set-ItemProperty $runKey -Name DesktopStyler -Value $runBackup }
  [D]::SetIcons($iconsBackup)
  Get-Process DesktopStyler -ErrorAction SilentlyContinue | Stop-Process
  Start-Process $dev -ArgumentList '--tray'   # your development copy, with your settings
}
if ($fails) { Write-Host "$fails check(s) failed" -ForegroundColor Red; exit 1 } else { Write-Host 'All installer checks passed' -ForegroundColor Green }
