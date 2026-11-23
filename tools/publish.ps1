# Builds dist\DesktopStyler.exe (one self-contained file, no .NET needed) and dist\AstraDesk-Setup.exe.
# Give people the Setup file: a normal wizard (Start menu, optional desktop shortcut, uninstall from Settings > Apps).
# Usage: powershell -ExecutionPolicy Bypass -File tools\publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root 'dist'
# Code signing (removes the SmartScreen "unknown publisher" warning). Skipped unless ASTRA_SIGN_ARGS is set to signtool's
# options for your certificate, e.g. Azure Trusted Signing:
#   $env:ASTRA_SIGN_ARGS = '/fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib "C:\path\Azure.CodeSigning.Dlib.dll" /dmdf "C:\path\metadata.json"'
# signtool comes from the Windows SDK (or set ASTRA_SIGNTOOL to its path).
function Sign($file) {
  if (-not $env:ASTRA_SIGN_ARGS) { "unsigned: $(Split-Path $file -Leaf) (set ASTRA_SIGN_ARGS to sign)"; return }
  $tool = $env:ASTRA_SIGNTOOL
  if (-not $tool) { $tool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source }
  if (-not $tool) { $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName }
  if (-not $tool) { throw "signtool.exe not found: install the Windows SDK or set ASTRA_SIGNTOOL" }
  cmd /c "`"$tool`" sign $env:ASTRA_SIGN_ARGS `"$file`"" # through cmd so quoted paths inside ASTRA_SIGN_ARGS work
  if ($LASTEXITCODE -ne 0) { throw "signing failed: $file" }
}
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish (Join-Path $root 'DesktopStyler.csproj') -c Release -r win-x64 --self-contained true -o $dist `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Remove-Item (Join-Path $dist '*.xml') -ErrorAction SilentlyContinue # API docs from the WebView2 package; not needed to run
Sign (Join-Path $dist 'DesktopStyler.exe') # before packaging, so the installed exe is signed too
# Setup wizard (Inno Setup 6: winget install JRSoftware.InnoSetup) -> dist\AstraDesk-Setup.exe
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found: winget install JRSoftware.InnoSetup" }
& $iscc /Q (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
Sign (Join-Path $dist 'AstraDesk-Setup.exe') # shortcut: Inno's own uninstaller stays unsigned; add SignTool= in installer.iss if Windows ever flags it
# Ship to the site: the installer, plus version.txt that installed copies check daily for updates (see Updater in Program.cs)
$site = Join-Path $root 'landing\public'
Copy-Item (Join-Path $dist 'AstraDesk-Setup.exe') (Join-Path $site 'downloads') -Force
$version = (Get-Item (Join-Path $dist 'DesktopStyler.exe')).VersionInfo.ProductVersion.Split('+')[0]
Set-Content (Join-Path $site 'version.txt') $version -NoNewline -Encoding ascii
"site: version.txt = $version (deploy the landing site to release it)"
Get-ChildItem $dist | ForEach-Object { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
