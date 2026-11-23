# Builds dist\DesktopStyler.exe (one self-contained file, no .NET needed) and dist\AstraDesk-Setup.exe.
# Give people the Setup file: a normal wizard (Start menu, optional desktop shortcut, uninstall from Settings > Apps).
# Usage: powershell -ExecutionPolicy Bypass -File tools\publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish (Join-Path $root 'DesktopStyler.csproj') -c Release -r win-x64 --self-contained true -o $dist `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Remove-Item (Join-Path $dist '*.xml') -ErrorAction SilentlyContinue # API docs from the WebView2 package; not needed to run
# Setup wizard (Inno Setup 6: winget install JRSoftware.InnoSetup) -> dist\AstraDesk-Setup.exe
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found: winget install JRSoftware.InnoSetup" }
& $iscc /Q (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
Get-ChildItem $dist | ForEach-Object { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
