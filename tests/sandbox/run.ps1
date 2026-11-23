# Clean-PC installer test: starts Windows Sandbox with dist\ mapped in, runs inside.ps1 there, waits for the results.
# Needs the "Windows Sandbox" feature (Windows Pro). Usage: powershell -ExecutionPolicy Bypass -File tests\sandbox\run.ps1 [outDir]
param([string]$OutDir = (Join-Path $env:TEMP 'DesktopStyler-sandbox'))
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
if (-not (Test-Path "$env:windir\System32\WindowsSandbox.exe")) { throw 'Windows Sandbox is not enabled' }
if (-not (Test-Path "$root\dist\DesktopStyler.exe")) { throw 'run tools\publish.ps1 first' }
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory $OutDir | Out-Null
$wsb = Join-Path $OutDir 'test.wsb'
@"
<Configuration>
  <Networking>Enable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$root\dist</HostFolder><SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\dist</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$PSScriptRoot</HostFolder><SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\tests</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$OutDir</HostFolder><SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\out</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell -ExecutionPolicy Bypass -File C:\Users\WDAGUtilityAccount\Desktop\tests\inside.ps1</Command></LogonCommand>
</Configuration>
"@ | Set-Content $wsb -Encoding utf8
Start-Process $wsb
$results = Join-Path $OutDir 'results.txt'
for ($i = 0; $i -lt 120; $i++) { Start-Sleep 5; if ((Test-Path $results) -and ((Get-Content $results) -contains 'DONE')) { break } }
Get-Content $results -ErrorAction SilentlyContinue
Get-Process WindowsSandbox*, WindowsSandboxRemoteSession* -ErrorAction SilentlyContinue | Stop-Process -Force # close the disposable PC
if (-not (Test-Path $results) -or (Get-Content $results) -match '^FAIL') { exit 1 }
