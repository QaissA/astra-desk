# Smoke test: runs the real app and checks taskbar, custom bar, screensaver and quit behaviour on this machine.
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File tests\smoke.ps1 [-RestartExplorer]
# It briefly switches to the custom bar and shows the screensaver for a few seconds, then restores your settings.
# -RestartExplorer also restarts Windows Explorer (closes open folder windows) to check the custom bar survives it.
param([switch]$RestartExplorer, [switch]$IdleTest) # -IdleTest: leave the PC alone ~80 s for the screensaver end-to-end check
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
# the normal Release build (not the win-x64 copy that tools\publish.ps1 leaves next to it)
$exe = Get-ChildItem (Join-Path $root 'bin\Release') -Recurse -Filter DesktopStyler.exe | Where-Object FullName -notmatch '\\win-x64\\' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
$settings = Join-Path $env:LOCALAPPDATA 'DesktopStyler\settings.json'

Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(int a, int p, ref RECT r, int f);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
  public static bool WallpaperAttached(int pid) {   // the video window must be a real child of the desktop (Progman or WorkerW)
    bool ok = false;
    EnumWindows((top, l) => { EnumChildWindows(top, (c, l2) => { uint p; GetWindowThreadProcessId(c, out p);
      if (p == pid && GetParent(c) == top) ok = true; return true; }, IntPtr.Zero); return true; }, IntPtr.Zero);
    return ok;
  }
  public struct RECT { public int L, T, R, B; }
  public static RECT WorkArea() { var r = new RECT(); SystemParametersInfo(0x30, 0, ref r, 0); return r; }
  public static bool HasBar(int pid, out RECT bar) { IntPtr h; return FindBar(pid, out bar, out h); }
  public static bool FindBar(int pid, out RECT bar, out IntPtr hwnd) {   // visible layered window owned by the app = the custom bar
    RECT found = new RECT(); bool ok = false; IntPtr fh = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h) && (GetWindowLong(h, -20) & 0x80000) != 0) { GetWindowRect(h, out found); fh = h; ok = true; }
      return true; }, IntPtr.Zero);
    bar = found; hwnd = fh; return ok;
  }
  public static System.Collections.Generic.List<RECT> Bars(int pid) {   // every custom bar of the app (one per screen)
    var list = new System.Collections.Generic.List<RECT>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); RECT r;
      if (p == pid && IsWindowVisible(h) && (GetWindowLong(h, -20) & 0x80000) != 0 && GetWindowRect(h, out r)) list.Add(r);
      return true; }, IntPtr.Zero);
    list.Sort((a, b) => a.L.CompareTo(b.L)); return list;
  }
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  public static string Foreground() { var h = GetForegroundWindow(); var t = new System.Text.StringBuilder(256); var c = new System.Text.StringBuilder(256);
    GetWindowText(h, t, 256); GetClassName(h, c, 256); uint pid; GetWindowThreadProcessId(h, out pid);
    string proc = ""; try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
    return t + " [" + c + "] " + proc; }
  public static void Key(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }
  public static void WinTab() { keybd_event(0x5B, 0, 0, UIntPtr.Zero); keybd_event(0x09, 0, 0, UIntPtr.Zero); keybd_event(0x09, 0, 2, UIntPtr.Zero); keybd_event(0x5B, 0, 2, UIntPtr.Zero); }
  public static void Wheel(IntPtr h, int x, int y, int sx, int sy, int delta) {   // hover the button, then scroll on it
    PostMessage(h, 0x0200, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xFFFF)));
    System.Threading.Thread.Sleep(300);
    PostMessage(h, 0x020A, (IntPtr)(delta << 16), (IntPtr)((sy << 16) | (sx & 0xFFFF)));
  }
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
  public struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct LII { public uint cb, t; }
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII l);
  public static uint IdleSeconds() { var l = new LII { cb = 8 }; GetLastInputInfo(ref l); return ((uint)Environment.TickCount - l.t) / 1000; }
  public static void Nudge() { mouse_event(1, 1, 0, 0, UIntPtr.Zero); mouse_event(1, -1, 0, 0, UIntPtr.Zero); } // a tiny real mouse move
  public static void RealWheel(int sx, int sy, int delta) {   // move the real cursor onto the spot and turn the real wheel, then put the cursor back
    POINT old; GetCursorPos(out old); SetCursorPos(sx, sy); System.Threading.Thread.Sleep(400);
    mouse_event(0x0800, 0, 0, delta, UIntPtr.Zero); System.Threading.Thread.Sleep(600); SetCursorPos(old.X, old.Y); }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public static void CtrlAlt(byte key) {   // presses Ctrl + Alt + key like a user would
    keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 0, UIntPtr.Zero);
    keybd_event(key, 0, 2, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
  }
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public static void Click(IntPtr h, int x, int y) {   // a real left click as the bar receives it (client coordinates)
    var lp = (IntPtr)((y << 16) | (x & 0xFFFF));
    PostMessage(h, 0x0200, IntPtr.Zero, lp); PostMessage(h, 0x0201, (IntPtr)1, lp); PostMessage(h, 0x0202, IntPtr.Zero, lp);
  }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
  public static bool IconsVisible() {   // desktop icons = SysListView32 inside SHELLDLL_DefView
    IntPtr dv = IntPtr.Zero;
    EnumWindows((t, l) => { dv = FindWindowEx(t, IntPtr.Zero, "SHELLDLL_DefView", null); return dv == IntPtr.Zero; }, IntPtr.Zero);
    return IsWindowVisible(FindWindowEx(dv, IntPtr.Zero, "SysListView32", null));
  }
  public static bool HasFullscreen(int pid, int w, int h) {   // the screensaver: a visible window of the process covering the screen
    bool ok = false;
    EnumWindows((hw, l) => { uint p; GetWindowThreadProcessId(hw, out p); RECT r;
      if (p == pid && IsWindowVisible(hw) && GetWindowRect(hw, out r) && r.L <= 0 && r.T <= 0 && r.R >= w && r.B >= h) ok = true;
      return true; }, IntPtr.Zero);
    return ok;
  }
}
'@
Add-Type @'
using System; using System.Runtime.InteropServices;
// Speaker master volume through Core Audio, called directly through its function table (some audio drivers make the
// usual .NET COM cast fail with E_NOINTERFACE; the direct call works everywhere).
public static class RawVol {
  [DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int ctx, ref Guid iid, out IntPtr obj);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDefault(IntPtr self, int flow, int role, out IntPtr dev);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Activate(IntPtr self, ref Guid iid, int ctx, IntPtr p, out IntPtr obj);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetScalar(IntPtr self, out float v);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetScalar(IntPtr self, float v, IntPtr ctx);
  static T Slot<T>(IntPtr obj, int i) { var vt = Marshal.ReadIntPtr(obj); return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vt, i * IntPtr.Size)); }
  static IntPtr Endpoint() {
    var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"); var iidEnum = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
    IntPtr en; Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 23, ref iidEnum, out en));
    IntPtr dev; Marshal.ThrowExceptionForHR(Slot<GetDefault>(en, 4)(en, 0, 1, out dev));
    var iidVol = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    IntPtr vol; Marshal.ThrowExceptionForHR(Slot<Activate>(dev, 3)(dev, ref iidVol, 23, IntPtr.Zero, out vol));
    return vol;
  }
  public static float Get() { var v = Endpoint(); float f; Marshal.ThrowExceptionForHR(Slot<GetScalar>(v, 9)(v, out f)); return f; }
  public static void Set(float x) { var v = Endpoint(); Marshal.ThrowExceptionForHR(Slot<SetScalar>(v, 7)(v, x, IntPtr.Zero)); }
}
'@
[W]::SetProcessDPIAware() | Out-Null
$screenH = [W]::GetSystemMetrics(1); $screenW = [W]::GetSystemMetrics(0)
$fails = 0
function Check($ok, $name) { if ($ok) { Write-Host "PASS $name" -ForegroundColor Green } else { Write-Host "FAIL $name" -ForegroundColor Red; $script:fails++ } }
function TaskbarVisible { [W]::IsWindowVisible([W]::FindWindow('Shell_TrayWnd', $null)) }
$testLeft = @('icons', 'start', 'search', 'taskview', 'network', 'notifications', 'volume') # left zone, in this order
function Launch($mode, $opacity, [string[]]$extraArgs = @(), [hashtable]$set = @{}) {
  Get-Process DesktopStyler -ErrorAction SilentlyContinue | Stop-Process; Start-Sleep 1
  $j = Get-Content $settings -Raw | ConvertFrom-Json
  $j | Add-Member -Force TaskbarMode $mode; $j | Add-Member -Force BarOpacity $opacity
  # test layout: known buttons at the far left, so the click tests know where each one is
  $j | Add-Member -Force BarLeft $testLeft
  $j | Add-Member -Force BarCenter @(@($j.BarCenter) | Where-Object { $_ -and $testLeft -notcontains $_ })
  $j | Add-Member -Force BarRight @(@($j.BarRight) | Where-Object { $_ -and $testLeft -notcontains $_ })
  foreach ($k in $set.Keys) { $j | Add-Member -Force $k $set[$k] }
  ($j | ConvertTo-Json -Depth 5) | Set-Content $settings -Encoding utf8
  $p = Start-Process $exe -ArgumentList (@('--tray') + $extraArgs) -PassThru; Start-Sleep 5; $p
}
function ButtonX($k, $index) { [int]((30 + 44 * $index) * $k) } # centre of the n-th button in the left zone (8px pad + 44px buttons)
$log = Join-Path $env:LOCALAPPDATA 'DesktopStyler\log.txt'
function NewLog($mark) { @(Get-Content $log | Select-Object -Skip $mark) }
function LogSince([datetime]$t) { # log lines written at or after $t (timestamps are "yyyy-MM-dd HH:mm:ss.fff")
  $at = [datetime]::MinValue
  @(Get-Content $log | Where-Object { $_.Length -ge 19 -and [datetime]::TryParseExact($_.Substring(0, 19), 'yyyy-MM-dd HH:mm:ss', $null, 'None', [ref]$at) -and $at -ge $t.AddSeconds(-1) })
}

$st = Start-Process $exe -ArgumentList '--selftest' -PassThru -Wait
Check ($st.ExitCode -eq 0) "unit self-test (--selftest): $(Get-Content (Join-Path $env:LOCALAPPDATA 'DesktopStyler\selftest.txt'))"

New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
if (-not (Test-Path $settings)) { '{}' | Set-Content $settings }
$backup = Get-Content $settings -Raw
try {
  # 1. custom bar replaces the Windows taskbar
  $p = Launch 'Custom' 0
  Check (-not $p.HasExited) 'app starts in Custom mode'
  Check (-not (TaskbarVisible)) 'Windows taskbar is hidden'
  $b = New-Object W+RECT
  $hasBar = [W]::HasBar($p.Id, [ref]$b)
  Check $hasBar 'custom bar window exists'
  if ($hasBar) {
    Check ($b.B -eq $screenH -and $b.L -eq 0 -and $b.R -eq $screenW) "custom bar spans the bottom of the main screen ($($b.L),$($b.T),$($b.R),$($b.B))"
    Check ([W]::WorkArea().B -eq $b.T) "maximized windows stop above the bar (work area bottom $([W]::WorkArea().B), bar top $($b.T))"
  }

  # 2. Explorer re-showing its taskbar gets undone
  [W]::ShowWindow([W]::FindWindow('Shell_TrayWnd', $null), 8) | Out-Null; Start-Sleep 1.5
  Check (-not (TaskbarVisible)) 'Windows taskbar is re-hidden if Explorer shows it'

  # 2b. clicking a custom bar button works: "Desktop icons" (left zone, first button) toggles the icons twice
  $hb = [IntPtr]::Zero
  if ([W]::FindBar($p.Id, [ref]$b, [ref]$hb)) {
    $k = ($b.B - $b.T) / 48.0; $x = [int](30 * $k); $y = [int](($b.B - $b.T) / 2)
    $before = [W]::IconsVisible()
    [W]::Click($hb, $x, $y); Start-Sleep 1.5; $mid = [W]::IconsVisible()
    [W]::Click($hb, $x, $y); Start-Sleep 1.5; $after = [W]::IconsVisible()
    Check ($mid -ne $before -and $after -eq $before) "clicking a custom bar button works (desktop icons $before -> $mid -> $after)"

    # each panel button opens its Windows panel (then Esc closes it)
    # matched by the Windows process that hosts each panel (on recent Windows 11 builds Start is hosted by SearchHost too,
    # which is exactly what the real Windows key opens)
    $panels = @(
      @{ i = 1; name = 'Start';          expect = 'StartMenuExperienceHost|SearchHost' },
      @{ i = 2; name = 'Search';         expect = 'SearchHost' },
      @{ i = 3; name = 'Task view';      expect = 'Task View|XamlExplorerHostIslandWindow' },
      @{ i = 4; name = 'Network';        expect = 'Quick settings|ControlCenter' },
      @{ i = 5; name = 'Notifications';  expect = 'ShellExperienceHost|Notification Center' })
    foreach ($pn in $panels) {
      [W]::Click($hb, (ButtonX $k $pn.i), $y)
      $fg = ''; for ($w = 0; $w -lt 12 -and $fg -notmatch $pn.expect; $w++) { Start-Sleep -Milliseconds 400; $fg = [W]::Foreground() } # panels open at different speeds
      Check ($fg -match $pn.expect) "custom bar '$($pn.name)' button opens its panel (foreground: $fg)"
      # close it and wait until it's really gone, so the next panel isn't confused with this one
      for ($w = 0; $w -lt 8 -and [W]::Foreground() -match $pn.expect; $w++) {
        if ($w -ge 3 -and $pn.name -eq 'Task view') { [W]::WinTab() } else { [W]::Key(0x1B) } # Task View sometimes ignores Esc; Win+Tab toggles it off
        Start-Sleep -Milliseconds 600
      }
      Start-Sleep 1
    }

    # scrolling on the volume button really changes the speaker volume: down, then up again; your exact level is put back
    $vol0 = [RawVol]::Get(); if ($vol0 -lt 0.1) { [RawVol]::Set(0.5); Start-Sleep 1 } # room to go down
    $start = [RawVol]::Get(); $vx = ButtonX $k 6
    [W]::RealWheel($b.L + $vx, $b.T + $y, -120); Start-Sleep 1; $down = [RawVol]::Get()
    [W]::RealWheel($b.L + $vx, $b.T + $y, 120); Start-Sleep 1; $up = [RawVol]::Get()
    [RawVol]::Set($vol0)
    Check ($down -lt $start -and $up -gt $down) ("scrolling on the volume button changes the speaker volume ({0:P0} -> {1:P0} -> {2:P0}, restored to {3:P0})" -f $start, $down, $up, [RawVol]::Get())
  }

  # 2c. optional: Explorer restart (Explorer forgets app bars and shows its taskbar again)
  if ($RestartExplorer) {
    Stop-Process -Name explorer -Force; Start-Sleep 2
    if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }
    Start-Sleep 8
    Check ([W]::HasBar($p.Id, [ref]$b)) 'custom bar survives an Explorer restart'
    Check (-not (TaskbarVisible)) 'Windows taskbar stays hidden after an Explorer restart'
    Check ([W]::WorkArea().B -eq $b.T) "space is reserved again after an Explorer restart (work area bottom $([W]::WorkArea().B), bar top $($b.T))"
  } else { Write-Host 'SKIP Explorer restart check (run with -RestartExplorer)' }

  # 3. crash recovery: killed app, then started again in Normal mode
  Stop-Process -Id $p.Id; Start-Sleep 1
  $p = Launch 'Normal' 35
  Check (TaskbarVisible) 'Normal mode brings the Windows taskbar back'
  Check (-not [W]::HasBar($p.Id, [ref]$b)) 'no custom bar in Normal mode'
  Check ([W]::WorkArea().B -lt $screenH) "Windows taskbar space is reserved again (work area bottom $([W]::WorkArea().B))"

  # 4. quitting (DesktopStyler.exe --quit) from Custom mode cleans up and brings the Windows taskbar back
  $p = Launch 'Custom' 0
  Start-Process $exe -ArgumentList '--quit' -Wait; Start-Sleep 2
  Check ($p.HasExited) 'quit closes the app'
  Check (TaskbarVisible) 'quit brings the Windows taskbar back'
  Check ([W]::WorkArea().B -lt $screenH) "quit gives back the custom bar's space (work area bottom $([W]::WorkArea().B))"
  # 4b. multi-monitor: --test-screens 2 splits this screen into two fake monitors; each gets its own bar and wallpaper,
  #     the clock only on the main one (unless "clock on every screen" is on)
  $mark = (Get-Content $log).Count
  $p = Launch 'Custom' 0 @('--test-screens', '2') @{ ClockStyle = 'neon'; ClockAllScreens = $false; WallpaperEnabled = $true }
  $bars = [W]::Bars($p.Id)
  $half = [int]($screenW / 2)
  Check ($bars.Count -eq 2) "multi-monitor: one custom bar per screen ($($bars.Count) bars)"
  if ($bars.Count -eq 2) {
    Check ($bars[0].L -eq 0 -and $bars[0].R -eq $half -and $bars[1].L -eq $half -and $bars[1].R -eq $screenW -and $bars[0].B -eq $screenH -and $bars[1].B -eq $screenH) `
      "multi-monitor: each bar sits at the bottom of its own screen ($($bars[0].L)-$($bars[0].R), $($bars[1].L)-$($bars[1].R))"
  }
  Start-Sleep 3
  $pages = NewLog $mark | Where-Object { $_ -match 'wallpaper page loading' }
  Check ((@($pages | Where-Object { $_ -match 'clock=yes' }).Count -eq 1) -and (@($pages | Where-Object { $_ -match 'clock=no' }).Count -eq 1)) `
    "multi-monitor: a wallpaper per screen, clock on the main screen only ($(@($pages).Count) pages)"
  $mark = (Get-Content $log).Count
  $p = Launch 'Normal' 35 @('--test-screens', '2') @{ ClockStyle = 'neon'; ClockAllScreens = $true; WallpaperEnabled = $true }
  Start-Sleep 3
  Check (@(NewLog $mark | Where-Object { $_ -match 'wallpaper page loading, clock=yes' }).Count -eq 2) 'multi-monitor: "clock on every screen" puts it on both'

  $p = Launch 'Normal' 35

  # 4c. pause during a full-screen app: open a real full-screen window in front, the wallpaper must pause for that reason
  Add-Type -AssemblyName System.Windows.Forms
  $mark = (Get-Content $log).Count
  $fs = New-Object Windows.Forms.Form -Property @{ FormBorderStyle = 'None'; StartPosition = 'Manual'; TopMost = $true; BackColor = 'Black'; ShowInTaskbar = $false }
  $fs.Bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
  $fs.Show(); $fs.Activate(); [W]::SetForegroundWindow($fs.Handle) | Out-Null
  for ($i = 0; $i -lt 40; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
  $fs.Close(); for ($i = 0; $i -lt 30; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
  $changes = NewLog $mark | Where-Object { $_ -match ' wallpaper (playing|paused)' }
  Check (@($changes | Where-Object { $_ -match 'full-screen' }).Count -ge 1 -and $changes[-1] -notmatch 'full-screen') `
    "wallpaper pauses while a full-screen app is in front and resumes after (now: $((@($changes)[-1] -split ' wallpaper ')[-1]))"

  # 5. a second copy must not start a second bar
  $p2 = Start-Process $exe -ArgumentList '--tray' -PassThru; Start-Sleep 2
  Check ($p2.HasExited) 'a second copy exits (single instance)'

  # 5b. keyboard shortcut + pause: Ctrl+Alt+P pauses the wallpaper, pressing it again resumes (read from the app's log)
  $log = Join-Path $env:LOCALAPPDATA 'DesktopStyler\log.txt'
  $mark = (Get-Content $log -ErrorAction SilentlyContinue).Count
  [W]::CtrlAlt(0x50); Start-Sleep 2; [W]::CtrlAlt(0x50); Start-Sleep 2
  $new = @(Get-Content $log | Select-Object -Skip $mark | Where-Object { $_ -match ' wallpaper (playing|paused)' })
  $byYou = [array]::FindIndex([string[]]$new, [Predicate[string]]{ param($l) $l -match 'paused by you' })
  # after the 2nd press it leaves "paused by you": playing, or paused for another reason (e.g. on battery)
  Check ($byYou -ge 0 -and $byYou -lt $new.Count - 1 -and $new[-1] -notmatch 'paused by you') "Ctrl+Alt+P pauses and resumes the wallpaper (now: $(($new[-1] -split ' wallpaper ')[-1]))"

  # 5c. playlist: Ctrl+Alt+W switches to another wallpaper from the playlist folder (settings are restored at the end)
  $videos = Join-Path ([Environment]::GetFolderPath('MyVideos')) 'Wallpapers'
  $media = @(Get-ChildItem $videos -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -match '^\.(mp4|webm|mov|mkv|gif|jpg|jpeg|png|webp)$' })
  $folder = (Get-Content $settings -Raw | ConvertFrom-Json).PlaylistFolder
  if (-not $folder -and $media.Count -ge 2) {
    $before = (Get-Content $settings -Raw | ConvertFrom-Json).Wallpaper
    [W]::CtrlAlt(0x57); Start-Sleep 3
    $after = (Get-Content $settings -Raw | ConvertFrom-Json).Wallpaper
    Check ($after -ne $before -and $after -like "$videos\*") "Ctrl+Alt+W switches to the next wallpaper ($(Split-Path $after -Leaf))"
  } else { Write-Host 'SKIP playlist check (needs 2+ wallpapers in Videos\Wallpapers and the default playlist folder)' }

  # 6. screensaver: "DesktopStyler /s" (what Windows runs) covers the screen; ended here after a few seconds
  $ss = Start-Process $exe -ArgumentList '/s' -PassThru; $covered = $false
  for ($i = 0; $i -lt 30 -and -not $covered -and -not $ss.HasExited; $i++) { Start-Sleep -Milliseconds 500; $covered = [W]::HasFullscreen($ss.Id, $screenW, $screenH) } # slower right after an Explorer restart
  Check $covered "screensaver covers the screen (process $(if ($ss.HasExited) { 'exited early' } else { 'running' }))"
  if (-not $ss.HasExited) { $ss.Kill() }
  Check (Test-Path (Join-Path $env:LOCALAPPDATA 'DesktopStyler\screensaver.html')) 'screensaver page was generated'

  # 6b. registered with Windows: turning it on registers DesktopStyler.scr, and Windows itself starts it
  $p = Launch 'Normal' 35 @() @{ ScreensaverOn = $true; ScreensaverMinutes = 5; ScreensaverLock = $false } # no sign-in, so the test can't lock the PC
  $desk = Get-ItemProperty 'HKCU:\Control Panel\Desktop'
  $scr = [IO.Path]::ChangeExtension($exe, '.scr')
  Check ($desk.'SCRNSAVE.EXE' -eq $scr -and (Test-Path $scr) -and $desk.ScreenSaveTimeOut -eq '300') "screensaver registered with Windows ($(Split-Path $desk.'SCRNSAVE.EXE' -Leaf), $($desk.ScreenSaveTimeOut) s)"
  # 6c. end-to-end idle test (screensaver timeout shortened to 12 s with --test-idle): nobody touches the PC; the app pauses
  #     the wallpaper just before, starts the screensaver at 12 s, and a mouse move ends it. Needs ~15 s without input.
  if ($IdleTest) {
    $since = Get-Date
    $p = Launch 'Normal' 35 @('--test-idle', '12') @{ ScreensaverOn = $true; ScreensaverMinutes = 1; ScreensaverLock = $false }
    for ($try = 1; $try -le 3; $try++) { # retried if someone used the PC during the wait
      if ($try -gt 1) { $since = Get-Date }
      Write-Host "Idle test (try $try): please don't touch the mouse or keyboard for ~15 seconds..." -ForegroundColor Yellow
      $saver = $null; $maxIdle = 0
      for ($i = 0; $i -lt 30 -and -not $saver; $i++) {
        Start-Sleep -Milliseconds 700; $maxIdle = [Math]::Max($maxIdle, [W]::IdleSeconds())
        $saver = Get-Process | Where-Object { $_.Path -like '*DesktopStyler.scr' }
      }
      if ($saver -or $maxIdle -ge 14) { break }
      Write-Host "  (the PC was used during the wait: longest idle $maxIdle s)"
    }
    $idleLog = LogSince $since
    Check (@($idleLog | Where-Object { $_ -match "paused: you're away" }).Count -ge 1) "idle: the wallpaper pauses when you are away (longest idle $maxIdle s)"
    $started = @($idleLog | Where-Object { $_ -match 'idle (\d+)s: starting the screensaver' }) | Select-Object -First 1
    Check ($null -ne $saver -and $started) "idle: the screensaver starts at the timeout ($(($started -split ' ',3)[2]))"
    if ($saver) { Start-Sleep 2; Check ([W]::HasFullscreen($saver[0].Id, $screenW, $screenH)) 'idle: the screensaver covers the screen' }
    if ($saver) {
      Start-Sleep 3; [W]::Nudge(); Start-Sleep 3
      Check ($saver[0].HasExited) 'idle: moving the mouse ends the screensaver'
      if (-not $saver[0].HasExited) { $saver | Stop-Process -Force }
    }
  } else { Write-Host 'SKIP idle end-to-end test (run with -IdleTest and leave the PC alone ~80 s)' }
}
finally {
  Get-Process DesktopStyler -ErrorAction SilentlyContinue | Stop-Process; Start-Sleep 1
  $backup | Set-Content $settings -Encoding utf8
  Start-Process $exe -ArgumentList '--tray'   # back to your own settings
  Start-Sleep 4
}
$saved = ConvertFrom-Json $backup
Check ((TaskbarVisible) -or ($saved.TaskbarMode -eq 'Custom')) 'your taskbar is restored afterwards'
$nowScr = (Get-ItemProperty 'HKCU:\Control Panel\Desktop').'SCRNSAVE.EXE'
Check ($saved.ScreensaverOn -or $nowScr -notlike '*DesktopStyler*') 'your screensaver setting is restored afterwards'
if ($saved.WallpaperEnabled -and $saved.Wallpaper -and (Test-Path -LiteralPath $saved.Wallpaper)) {
  Check ([W]::WallpaperAttached((Get-Process DesktopStyler).Id)) 'animated wallpaper is attached behind the desktop icons'
} else { Write-Host 'SKIP animated wallpaper check (no wallpaper enabled in your settings)' }
if ($fails) { Write-Host "$fails check(s) failed" -ForegroundColor Red; exit 1 } else { Write-Host 'All checks passed' -ForegroundColor Green }
