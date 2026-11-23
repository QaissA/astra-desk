# Runs INSIDE Windows Sandbox (a clean Windows): the shipped installer on a PC that has never seen Astra Desk.
# Started by tests\sandbox\run.ps1; writes results.txt and screenshots to the mapped "out" folder.
$ErrorActionPreference = 'Continue'
$in = 'C:\Users\WDAGUtilityAccount\Desktop\dist\DesktopStyler.exe'
$out = 'C:\Users\WDAGUtilityAccount\Desktop\out'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\DesktopStyler\DesktopStyler.exe'
$lines = New-Object System.Collections.Generic.List[string]
function Check($ok, $name) { $lines.Add($(if ($ok) { "PASS $name" } else { "FAIL $name" })); $lines | Set-Content (Join-Path $out 'results.txt') }

try {
  Check (Test-Path $in) 'installer reached the clean PC'
  $os = Get-CimInstance Win32_OperatingSystem
  $lines.Add("INFO clean PC: $($os.Caption) $($os.Version), .NET runtimes: $(@(Get-ChildItem 'C:\Program Files\dotnet\shared' -ErrorAction SilentlyContinue).Count) (0 = none installed)")
  $wv = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' -ErrorAction SilentlyContinue
  $lines.Add("INFO WebView2 runtime: $(if ($wv) { $wv.pv } else { 'not registered (checked by the app itself)' })")

  $st = Start-Process $in -ArgumentList '--selftest' -PassThru -Wait
  Check ($st.ExitCode -eq 0) "self-test on the clean PC: $(Get-Content (Join-Path $env:LOCALAPPDATA 'DesktopStyler\selftest.txt') -ErrorAction SilentlyContinue)"

  Start-Process $in -ArgumentList '--install', '--quiet' -Wait
  Check (Test-Path $installed) 'installs on the clean PC'
  Check (Test-Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'Astra Desk.lnk')) 'Start menu entry created'
  Check ([bool](Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopStyler' -ErrorAction SilentlyContinue)) 'listed in Settings > Apps'

  # full settings-window pass on the clean PC (first run: welcome screen, live Wallhaven search, screenshots)
  $ui = Start-Process $installed -ArgumentList '--uitest', (Join-Path $out 'uitest') -PassThru
  if (-not $ui.WaitForExit(240000)) { $ui.Kill() }
  $report = Get-Content (Join-Path $out 'uitest\report.txt') -ErrorAction SilentlyContinue
  Check ($report -match 'welcome overlay shown: true') 'first run shows the welcome screen'
  Check (($report -match 'shown=true').Count -eq 6) 'all 6 settings pages open'
  Check ($report -match 'script errors: "\[\]"') 'no script errors'
  $lines.Add("INFO " + (($report | Where-Object { $_ -match 'workshop results' }) -join ''))

  $run = Start-Process $installed -ArgumentList '--tray' -PassThru; Start-Sleep 10
  Check (-not $run.HasExited) 'installed app keeps running'
  Start-Process $installed -ArgumentList '--quit' -Wait; Start-Sleep 3
  Check ($run.HasExited) 'quits cleanly'

  Start-Process $installed -ArgumentList '--uninstall', '--quiet', '--purge' -Wait; Start-Sleep 6
  Check (-not (Test-Path (Split-Path $installed))) 'uninstall removes the program'
  Check (-not (Test-Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'Astra Desk.lnk'))) 'uninstall removes the Start menu entry'
  Check (-not (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopStyler' -ErrorAction SilentlyContinue)) 'uninstall removes the Settings > Apps entry'
  Check (-not (Test-Path (Join-Path $env:LOCALAPPDATA 'DesktopStyler'))) 'uninstall with --purge leaves no settings behind'
  Check ([bool](Get-Process explorer -ErrorAction SilentlyContinue)) 'Windows shell fine after uninstall'
}
catch { $lines.Add("FAIL crashed: $_") }
$lines.Add('DONE')
$lines | Set-Content (Join-Path $out 'results.txt')
