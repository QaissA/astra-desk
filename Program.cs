using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace DesktopStyler;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--selftest")) // no console in a Windows app: the result goes to selftest.txt as well as the exit code
        {
            var result = Path.Combine(Settings.Dir, "selftest.txt");
            Directory.CreateDirectory(Settings.Dir);
            try { SelfTest.Run(); File.WriteAllText(result, "PASS"); }
            catch (Exception e) { File.WriteAllText(result, e.Message); Environment.Exit(1); }
            return;
        }
        if (Screensaver.IsScreensaverArgs(args)) { Screensaver.Run(args); return; } // started by Windows as DesktopStyler.scr
        if (args.Contains("--install")) { Installer.Install(quiet: args.Contains("--quiet")); return; }
        if (args.Contains("--uninstall")) { Installer.Uninstall(quiet: args.Contains("--quiet"), purge: args.Contains("--purge")); return; }
        var split = Array.IndexOf(args, "--test-screens");
        if (split >= 0 && split + 1 < args.Length && int.TryParse(args[split + 1], out var n)) Display.TestSplit = Math.Clamp(n, 2, 4);
        var idleArg = Array.IndexOf(args, "--test-idle");
        if (idleArg >= 0 && idleArg + 1 < args.Length && int.TryParse(args[idleArg + 1], out var secs)) App.TestIdleSeconds = Math.Clamp(secs, 5, 600);
        if (!WebViewReady()) return;
        if (args.Contains("--uitest")) // test pass of the settings window: screenshots + report, then quits (see MainForm.RunUiTest)
        {
            MainForm.UiTestDir = args.SkipWhile(a => a != "--uitest").Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(Settings.Dir, "uitest");
            MainForm.UiTestDownload = args.Contains("--download");
        }
        if (Installer.OfferInstall(args)) return; // first run of a downloaded copy: install it, the installed copy takes over
        using var mutex = new Mutex(true, @"Local\DesktopStyler", out bool first);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DesktopStyler.Show");
        using var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DesktopStyler.Quit");
        if (args.Contains("--quit")) { if (!first) quitSignal.Set(); return; } // `DesktopStyler.exe --quit` closes the running copy cleanly
        if (!first) { if (!args.Contains("--tray")) showSignal.Set(); return; } // launching it again opens the running copy's settings
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        try { Application.Run(new App(showWindow: !args.Contains("--tray"), showSignal, quitSignal)); }
        finally { Taskbar.Unhide(); } // never leave the user without a taskbar
    }

    /// The settings window and wallpaper need Microsoft's WebView2 runtime (built into Windows 11; older PCs may lack it).
    static bool WebViewReady()
    {
        try { CoreWebView2Environment.GetAvailableBrowserVersionString(); return true; }
        catch (WebView2RuntimeNotFoundException)
        {
            if (MessageBox.Show("Astra Desk needs the Microsoft Edge WebView2 Runtime, which isn't installed on this PC.\n\nOpen the download page now?",
                    "Astra Desk", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
            return false;
        }
    }
}

/// Animated screensaver. Windows starts DesktopStyler.scr (a hard link to this exe) with /s after the idle time;
/// it plays the same page as the desktop wallpaper, full screen, and closes on any key or mouse movement.
static class Screensaver
{
    const string DesktopKey = @"HKEY_CURRENT_USER\Control Panel\Desktop";
    public static string ScrPath => Path.ChangeExtension(Environment.ProcessPath, ".scr");
    public static bool IsScreensaverArgs(string[] args) => args.Length > 0 && Regex.IsMatch(args[0], "^/[scpSCP](:\\d*)?$");

    public static void Run(string[] args)
    {
        var mode = char.ToLowerInvariant(args[0][1]);
        if (mode == 'p') return; // the tiny preview box in Windows' dialog isn't supported
        if (mode == 'c') { Process.Start(new ProcessStartInfo(Path.ChangeExtension(Environment.ProcessPath, ".exe")) { UseShellExecute = true }); return; } // "Settings": open the app
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        var s = Settings.Load();
        var media = WallpaperHost.PreviewMedia(s) ?? Tweaks.StillWallpaper();
        Directory.CreateDirectory(Settings.Dir);
        File.WriteAllText(Path.Combine(Settings.Dir, "screensaver.html"), WallpaperHost.Html(s, media, animate: media == s.Wallpaper));
        using var running = new Mutex(true, RunningName, out bool firstSaver);
        if (!firstSaver) return; // one screensaver at a time (Windows and the app may both start it)
        var forms = Display.All().Select(sc => new SaverForm(sc, media, sc.Primary || s.ClockAllScreens)).ToList();
        foreach (var f in forms.Skip(1)) f.Show();
        // the web view swallows input, so watch the system-wide "last input" time instead
        long start = Environment.TickCount64; uint baseline = LastInput();
        bool endedByInput = false, screenOff = false;
        var t = new System.Windows.Forms.Timer { Interval = 100 };
        t.Tick += (_, _) =>
        {
            if (Environment.TickCount64 - start < 1500) { baseline = LastInput(); return; } // ignore the input that may have just started it
            if (LastInput() != baseline) { endedByInput = true; Application.Exit(); return; }
            // the screensaver's video keeps the screen awake, so it applies the power plan's screen-off timeout itself
            int off = IdleTimeouts.ScreenOffSeconds(SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline);
            if (!screenOff && off > 0 && IdleTimeouts.IdleSeconds() >= off) { screenOff = true; IdleTimeouts.TurnScreenOff(); }
        };
        t.Start();
        Cursor.Hide();
        Application.Run(forms[0]);
        if (endedByInput && s.ScreensaverLock) Native.LockWorkStation(); // "Ask for sign-in when it ends"
    }

    const string RunningName = @"Local\DesktopStyler.Screensaver";

    public static bool IsRunning() { if (Mutex.TryOpenExisting(RunningName, out var m)) { m.Dispose(); return true; } return false; }

    /// Starts the screensaver now (used when the idle time is reached; Windows' own timer would be a timeout late).
    public static void Start()
    {
        var path = File.Exists(ScrPath) ? ScrPath : Environment.ProcessPath;
        Process.Start(new ProcessStartInfo(path, "/s") { UseShellExecute = false });
    }

    static uint LastInput() { var i = new Native.LASTINPUTINFO { cbSize = 8 }; Native.GetLastInputInfo(ref i); return i.dwTime; }

    class SaverForm : Form
    {
        public SaverForm(Display screen, string media, bool clock)
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Bounds = screen.Bounds;
            TopMost = true; ShowInTaskbar = false; BackColor = Color.Black;
            var wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(wv);
            Load += async (_, _) =>
            {
                try
                {
                    await wv.EnsureCoreWebView2Async(await Web.Env());
                    wv.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local", Settings.Dir, CoreWebView2HostResourceAccessKind.Allow);
                    if (!string.IsNullOrEmpty(media))
                        wv.CoreWebView2.SetVirtualHostNameToFolderMapping("media.local", Path.GetDirectoryName(media), CoreWebView2HostResourceAccessKind.Allow);
                    wv.CoreWebView2.Navigate("https://app.local/screensaver.html" + (clock ? "?main" : ""));
                }
                catch (Exception e) { WallpaperHost.Log("screensaver failed: " + e.Message); Application.Exit(); }
            };
        }
    }

    /// Registers (or unregisters) Astra Desk as the Windows screensaver; leaves other screensavers alone.
    public static void Apply(Settings s)
    {
        bool mine = string.Equals(Registry.GetValue(DesktopKey, "SCRNSAVE.EXE", "") as string, ScrPath, StringComparison.OrdinalIgnoreCase);
        if (s.ScreensaverOn)
        {
            RefreshScr();
            int seconds = Math.Clamp(s.ScreensaverMinutes, 1, 120) * 60;
            Registry.SetValue(DesktopKey, "SCRNSAVE.EXE", ScrPath);
            Registry.SetValue(DesktopKey, "ScreenSaveTimeOut", seconds.ToString());
            Registry.SetValue(DesktopKey, "ScreenSaverIsSecure", s.ScreensaverLock ? "1" : "0");
            Registry.SetValue(DesktopKey, "ScreenSaveActive", "1");
            Native.SystemParametersInfo(15, (uint)seconds, null, 2);              // SPI_SETSCREENSAVETIMEOUT
            Native.SystemParametersInfo(0x77, s.ScreensaverLock ? 1u : 0u, null, 2); // SPI_SETSCREENSAVESECURE
            Native.SystemParametersInfo(17, 1, null, 2);                          // SPI_SETSCREENSAVEACTIVE
        }
        else if (mine)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)) k?.DeleteValue("SCRNSAVE.EXE", false);
            Registry.SetValue(DesktopKey, "ScreenSaveActive", "0");
            Native.SystemParametersInfo(17, 0, null, 2);
        }
    }

    /// DesktopStyler.scr next to the exe: a hard link (no extra disk space), re-made when the exe was rebuilt or updated.
    static void RefreshScr()
    {
        string exe = Environment.ProcessPath, scr = ScrPath;
        if (File.Exists(scr) && File.GetLastWriteTimeUtc(scr) == File.GetLastWriteTimeUtc(exe) && new FileInfo(scr).Length == new FileInfo(exe).Length) return;
        try { File.Delete(scr); } catch { return; } // in use right now: keep the old one
        if (!Native.CreateHardLink(scr, exe, 0)) File.Copy(exe, scr, true);
    }

    /// "Preview" button: run the screensaver now.
    public static void Preview() => Process.Start(new ProcessStartInfo(Environment.ProcessPath, "/s") { UseShellExecute = false });
}

/// Per-user install to %LOCALAPPDATA%\Programs\DesktopStyler with Start menu / desktop shortcuts and an entry in
/// Settings > Apps (no admin rights needed). Uninstall puts Windows back the way it was.
static class Installer
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopStyler";
    public static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DesktopStyler");
    public static string InstalledExe => Path.Combine(InstallDir, "DesktopStyler.exe");
    static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Astra Desk.lnk");
    static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Astra Desk.lnk");
    public static string Version => Application.ProductVersion.Split('+')[0];

    static bool IsDevBuild => Environment.ProcessPath.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase);
    public static bool IsInstalled => string.Equals(Path.GetDirectoryName(Environment.ProcessPath), InstallDir, StringComparison.OrdinalIgnoreCase);

    /// Downloaded copy started for the first time: offer to install. Returns true when this process should exit.
    public static bool OfferInstall(string[] args)
    {
        if (IsDevBuild || IsInstalled || args.Length > 0) return false;
        var answer = MessageBox.Show(
            "Install Astra Desk on this PC?\n\nYes: install it (Start menu, desktop shortcut, uninstall from Settings > Apps).\nNo: just run it this time.",
            "Astra Desk " + Version, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return true;
        if (answer == DialogResult.No) return false;
        Install(quiet: false);
        return true;
    }

    public static void Install(bool quiet)
    {
        StopRunningCopies();
        Directory.CreateDirectory(InstallDir);
        if (!string.Equals(Environment.ProcessPath, InstalledExe, StringComparison.OrdinalIgnoreCase)) File.Copy(Environment.ProcessPath, InstalledExe, true);
        Tweaks.CreateShortcut(StartMenuLink, InstalledExe);
        Tweaks.CreateShortcut(DesktopLink, InstalledExe);
        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", "Astra Desk");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "Astra Desk");
            k.SetValue("DisplayIcon", InstalledExe);
            k.SetValue("InstallLocation", InstallDir);
            k.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
            k.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet");
            k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        if (!quiet) Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
    }

    /// Restores the Windows taskbar, desktop icons, startup and screensaver settings, removes shortcuts and the app.
    /// Settings are kept unless the user agrees (or --purge); downloaded wallpapers in Videos\Wallpapers are always kept.
    public static void Uninstall(bool quiet, bool purge)
    {
        if (!quiet && MessageBox.Show("Remove Astra Desk?\n\nYour Windows taskbar, wallpaper and desktop go back to normal. Wallpapers you downloaded stay in Videos\\Wallpapers.",
                "Astra Desk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        StopRunningCopies();
        Taskbar.Apply("Normal"); Taskbar.Unhide();
        Tweaks.SetDesktopIcons(true);
        Tweaks.SetStartWithWindows(false);
        Screensaver.Apply(new Settings { ScreensaverOn = false });
        foreach (var lnk in new[] { StartMenuLink, DesktopLink }) try { File.Delete(lnk); } catch { }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        if (!quiet && !purge)
            purge = MessageBox.Show("Also delete your Astra Desk settings?", "Astra Desk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        var targets = new List<string>();
        if (Directory.Exists(InstallDir)) targets.Add(InstallDir);
        if (purge && Directory.Exists(Settings.Dir)) targets.Add(Settings.Dir);
        if (targets.Count > 0) // files in use (this exe, web cache): delete a moment after this process exits
            Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 3 /nobreak >nul & " + string.Join(" & ", targets.Select(t => $"rmdir /s /q \"{t}\"")))
                { CreateNoWindow = true, UseShellExecute = false });
        if (!quiet) MessageBox.Show("Astra Desk was removed.", "Astra Desk", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// Asks a running copy to quit (it restores the taskbar itself) and waits for it.
    static void StopRunningCopies()
    {
        using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DesktopStyler.Quit")) quit.Set();
        var me = Environment.ProcessId;
        foreach (var p in Process.GetProcessesByName("DesktopStyler").Where(p => p.Id != me))
            try { if (!p.WaitForExit(8000)) p.Kill(); } catch { }
    }
}

class Settings
{
    public string TaskbarMode { get; set; } = "Normal"; // Normal | AutoHide | Custom (Windows bar hidden, our Bar shown)
    public int BarOpacity { get; set; } = 35; // background darkness in %, 0 = fully transparent
    public List<string> BarLeft { get; set; } = [];
    public List<string> BarCenter { get; set; } = ["start", "search", "taskview", "pinned", "apps"];
    public List<string> BarRight { get; set; } = ["settings", "network", "volume", "battery", "clock", "notifications"];
    public List<string> PinnedApps { get; set; } = new();
    public string Wallpaper { get; set; } = "";
    public bool WallpaperEnabled { get; set; }
    public string WallpaperMotion { get; set; } = "none";
    public string WallpaperOverlay { get; set; } = "none";
    public string WallpaperSpeed { get; set; } = "normal";
    public string ClockStyle { get; set; } = "off"; // off | neon | minimal | digital | analog | glass
    public string ClockPosition { get; set; } = "center";
    public string ClockSize { get; set; } = "medium";
    public bool Clock24h { get; set; } = true;
    public bool ClockDate { get; set; } = true;
    public bool LockSync { get; set; } // copy the wallpaper's look to the lock screen whenever it changes
    public bool ClockAllScreens { get; set; }
    public bool PauseFullscreen { get; set; } = true; // games, full-screen video, presentations
    public bool PauseOnBattery { get; set; } = true;
    public bool PauseMaximized { get; set; }
    public string PlaylistFolder { get; set; } = ""; // "" = Videos\Wallpapers (where the Workshop saves)
    public int PlaylistMinutes { get; set; } // 0 = no rotation
    public bool PlaylistShuffle { get; set; } = true;
    public bool ScreensaverOn { get; set; }
    public int ScreensaverMinutes { get; set; } = 5;
    public bool ScreensaverLock { get; set; } = true; // sign-in required after the screensaver
    public bool Welcomed { get; set; } // first-run welcome was dismissed
    public string PexelsKey { get; set; } = "";  // free keys the user pastes in the Workshop; stored only on this PC
    public string PixabayKey { get; set; } = "";

    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopStyler");
    static string FilePath => Path.Combine(Dir, "settings.json");
    public static Settings Load() { try { return Parse(File.ReadAllText(FilePath)); } catch { return new(); } }

    public static Settings Parse(string json)
    {
        Settings s;
        try { s = JsonSerializer.Deserialize<Settings>(json) ?? new(); } catch { s = new(); } // corrupt file: start from defaults
        if (s.TaskbarMode == "Hidden") s.TaskbarMode = "Custom"; // old setting name
        return s;
    }

    /// Keeps only known elements, each in at most one zone (left wins, then center).
    public void SetZones(IEnumerable<string> left, IEnumerable<string> center, IEnumerable<string> right)
    {
        static List<string> Clean(IEnumerable<string> ids, IEnumerable<string> taken) =>
            ids.Where(id => Bar.Elements.Any(e => e.Id == id)).Distinct().Except(taken).ToList();
        BarLeft = Clean(left, []);
        BarCenter = Clean(center, BarLeft);
        BarRight = Clean(right, BarLeft.Concat(BarCenter));
    }
    public void Save() { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
}

class App : ApplicationContext
{
    public readonly Settings S = Settings.Load();
    readonly NotifyIcon tray;
    readonly WallpaperHost wall = new();
    readonly List<Bar> bars = new(); // one custom bar per screen
    readonly System.Windows.Forms.Timer pauseTimer = new() { Interval = 2000 };
    readonly System.Windows.Forms.Timer playlistTimer = new();
    readonly Random random = new();
    readonly Hotkeys hotkeys;
    MainForm form;

    public bool ManualPause { get; private set; }
    bool actedThisIdle; // screensaver started / screen turned off once per idle period
    public static int TestIdleSeconds; // `--test-idle N`: screensaver timeout of N seconds, for the end-to-end test
    public string PauseReason { get; private set; } // null = the wallpaper is playing
    public event Action Changed; // changed from outside the settings window (shortcut, playlist, tray): refresh it
    public IReadOnlyList<(string Keys, string Action, bool Ok)> Shortcuts => hotkeys.List;

    public App(bool showWindow, EventWaitHandle showSignal, EventWaitHandle quitSignal)
    {
        lockTimer.Tick += (_, _) => SyncLockScreen();
        pauseTimer.Tick += (_, _) => CheckPause();
        playlistTimer.Tick += (_, _) => NextWallpaper();
        var ui = new Control(); _ = ui.Handle; // marshals the signals below onto the UI thread
        ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => ui.BeginInvoke(ShowForm), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(quitSignal, (_, _) => ui.BeginInvoke(Quit), null, -1, true);
        try { Tweaks.EnsureStartMenuShortcut(); } catch { } // nice to have; never block startup
        if (Tweaks.StartsWithWindows) Tweaks.SetStartWithWindows(true); // re-point the startup entry if the exe moved
        tray = new NotifyIcon { Icon = AppIcon.Get(SystemInformation.SmallIconSize), Text = "Astra Desk", Visible = true, ContextMenuStrip = new ContextMenuStrip() };
        var m = tray.ContextMenuStrip.Items;
        m.Add("Astra Desk settings", null, (_, _) => ShowForm());
        m.Add("Next wallpaper", null, (_, _) => NextWallpaper());
        m.Add("Pause / resume wallpaper", null, (_, _) => TogglePause());
        m.Add(new ToolStripSeparator());
        m.Add("Tile windows: columns", null, (_, _) => Tiler.Apply("Columns"));
        m.Add("Tile windows: grid", null, (_, _) => Tiler.Apply("Grid"));
        m.Add("Tile windows: main + side", null, (_, _) => Tiler.Apply("Main"));
        m.Add("Show/hide desktop icons", null, (_, _) => Tweaks.SetDesktopIcons(!Tweaks.DesktopIconsVisible));
        m.Add("Custom bar on/off", null, (_, _) => ToggleBar());
        m.Add(new ToolStripSeparator());
        m.Add("Quit Astra Desk", null, (_, _) => Quit());
        tray.DoubleClick += (_, _) => ShowForm();

        hotkeys = new Hotkeys([
            (Keys.W, "Next wallpaper", NextWallpaper),
            (Keys.P, "Pause / resume wallpaper", TogglePause),
            (Keys.D, "Show / hide desktop icons", () => Tweaks.SetDesktopIcons(!Tweaks.DesktopIconsVisible)),
            (Keys.B, "Custom bar on / off", ToggleBar),
            (Keys.T, "Tile windows (grid)", () => Tiler.Apply("Grid")),
            (Keys.S, "Open Astra Desk", ShowForm),
        ]);

        ApplyTaskbar(); ApplyWallpaper(); ApplyPlaylist();
        try { Screensaver.Apply(S); } catch (Exception e) { WallpaperHost.Log("screensaver setup failed: " + e.Message); }
        pauseTimer.Start();
        SystemEvents.DisplaySettingsChanged += (_, _) => { ApplyTaskbar(); ApplyWallpaper(); };
        SystemEvents.PowerModeChanged += (_, _) => CheckPause();
        if (showWindow) ShowForm();
    }

    void ShowForm() { form ??= new MainForm(this); form.Show(); form.Activate(); }

    /// Closes the app: wallpaper stops, custom bar goes away, the Windows taskbar comes back (see ExitThreadCore).
    public void Quit() => ExitThread();

    void ToggleBar() { S.TaskbarMode = S.TaskbarMode == "Custom" ? "Normal" : "Custom"; ApplyTaskbar(); Changed?.Invoke(); }

    public void ApplyTaskbar()
    {
        foreach (var b in bars) b.Close();
        bars.Clear();
        Taskbar.Apply(S.TaskbarMode == "Custom" ? "Hidden" : S.TaskbarMode);
        if (S.TaskbarMode == "Custom")
            foreach (var screen in Display.All())
            {
                var b = new Bar(S, screen) { ContextMenuStrip = tray.ContextMenuStrip, OpenSettings = ShowForm };
                b.Show(); bars.Add(b);
            }
        S.Save();
    }

    public void RefreshBar() { foreach (var b in bars) b.Render(); S.Save(); }

    // ---- pause: saves CPU/GPU/battery when nobody can see the wallpaper or a game needs the power ----

    /// Why the wallpaper should be paused right now (null = play). quns = SHQueryUserNotificationState result.
    /// away = the user has been idle almost as long as Windows' screensaver / screen-off timeout: a playing video makes
    /// Windows keep the display on, so pausing lets the screensaver start and the screen turn off on time.
    public static string PauseReasonFor(Settings s, int quns, bool onBattery, bool maximized, bool manual, bool away = false) =>
        manual ? "paused by you"
        : quns == 1 ? "the screen is locked or a screensaver is running"
        : away ? "you're away, so Windows can start the screensaver or turn off the screen"
        : s.PauseFullscreen && quns is 2 or 3 or 4 ? "a full-screen app or game is running"
        : s.PauseOnBattery && onBattery ? "the PC is on battery"
        : s.PauseMaximized && maximized ? "a maximized window covers the desktop"
        : null;

    public void CheckPause()
    {
        Native.SHQueryUserNotificationState(out int quns);
        bool battery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        var fg = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(fg, out uint pid);
        bool maximized = fg != 0 && pid != Environment.ProcessId && Native.IsZoomed(fg) && Screen.FromHandle(fg).Primary;
        uint idle = IdleTimeouts.IdleSeconds();
        int saverSec = !S.ScreensaverOn ? 0 : TestIdleSeconds > 0 ? TestIdleSeconds : Math.Clamp(S.ScreensaverMinutes, 1, 120) * 60;
        int screenOff = IdleTimeouts.ScreenOffSeconds(battery);
        int awayAfter = IdleTimeouts.PauseAfter(saverSec, screenOff);
        bool away = awayAfter > 0 && idle >= awayAfter;
        if (idle < 3) actedThisIdle = false; // the user is back
        var action = IdleTimeouts.Action(idle, saverSec, screenOff, Screensaver.IsRunning(), actedThisIdle);
        if (action != null)
        {
            actedThisIdle = true;
            WallpaperHost.Log($"idle {idle}s: " + (action == "screensaver" ? "starting the screensaver" : "turning the screen off"));
            if (action == "screensaver") Screensaver.Start(); else IdleTimeouts.TurnScreenOff();
        }
        var reason = PauseReasonFor(S, quns, battery, maximized, ManualPause, away);
        wall.SetPaused(reason != null);
        if (reason == PauseReason) return;
        PauseReason = reason;
        WallpaperHost.Log("wallpaper " + (reason == null ? "playing" : "paused: " + reason));
        Changed?.Invoke();
    }

    public void TogglePause() { ManualPause = !ManualPause; CheckPause(); }

    // ---- playlist: rotate through a folder of wallpapers ----

    public string PlaylistFolder => string.IsNullOrWhiteSpace(S.PlaylistFolder) ? Workshop.Folder : S.PlaylistFolder;

    public List<string> PlaylistFiles() => Directory.Exists(PlaylistFolder)
        ? Directory.EnumerateFiles(PlaylistFolder).Where(SiteBrowser.IsMediaFile).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
        : [];

    /// Next file after `current` (or a random other one when shuffling); null when the folder is empty.
    public static string NextInPlaylist(IReadOnlyList<string> files, string current, bool shuffle, Random rnd)
    {
        if (files.Count == 0) return null;
        var others = files.Where(f => !string.Equals(f, current, StringComparison.OrdinalIgnoreCase)).ToList();
        if (others.Count == 0) return files[0];
        if (shuffle) return others[rnd.Next(others.Count)];
        int i = files.ToList().FindIndex(f => string.Equals(f, current, StringComparison.OrdinalIgnoreCase));
        return files[(i + 1) % files.Count]; // i = -1 (current not in folder) starts at the first file
    }

    public void ApplyPlaylist()
    {
        playlistTimer.Stop();
        if (S.PlaylistMinutes > 0) { playlistTimer.Interval = S.PlaylistMinutes * 60_000; playlistTimer.Start(); }
    }

    public void NextWallpaper()
    {
        var next = NextInPlaylist(PlaylistFiles(), S.Wallpaper, S.PlaylistShuffle, random);
        if (next != null) SetWallpaper(next);
    }

    /// Sets a file as the wallpaper; a still image gets a gentle motion so it's animated right away.
    public void SetWallpaper(string file)
    {
        S.Wallpaper = file; S.WallpaperEnabled = true;
        if (Path.GetExtension(file).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" && S.WallpaperMotion == "none") S.WallpaperMotion = "drift";
        ApplyWallpaper();
        Changed?.Invoke();
    }

    public void ApplyWallpaper()
    {
        WallpaperHost.Log($"apply wallpaper enabled={S.WallpaperEnabled} exists={File.Exists(S.Wallpaper)}");
        var media = WallpaperHost.MediaFor(S);
        var page = WallpaperHost.PreviewMedia(S); // also feeds the settings preview while the wallpaper is paused
        if (page != null)
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(Path.Combine(Settings.Dir, "wallpaper.html"), WallpaperHost.Html(S, page, animate: page == S.Wallpaper));
        }
        if (media != null) wall.Show(media, S.ClockAllScreens); else wall.Stop();
        S.Save();
        if (S.LockSync) { lockTimer.Stop(); lockTimer.Start(); } // debounced: waits for the new wallpaper to load first
        CheckPause(); // a fresh wallpaper starts paused if it should be
    }

    readonly System.Windows.Forms.Timer lockTimer = new() { Interval = 4000 };
    public event Action<string> LockScreenUpdated; // the settings window shows the result

    /// Puts the wallpaper's current look on the lock screen (which can only show a still image).
    public async Task UpdateLockScreen()
    {
        var png = await wall.CaptureForLockScreen();
        if (png == null)
        {
            var media = WallpaperHost.PreviewMedia(S);
            if (!string.IsNullOrEmpty(media) && Path.GetExtension(media).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png") png = media;
            else throw new InvalidOperationException("Turn the wallpaper on first, so its current look can be captured.");
        }
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(png);
        await Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);
    }

    async void SyncLockScreen()
    {
        lockTimer.Stop();
        try { await UpdateLockScreen(); WallpaperHost.Log("lock screen updated"); LockScreenUpdated?.Invoke(null); }
        catch (Exception e) { WallpaperHost.Log("lock screen sync failed: " + e.Message); LockScreenUpdated?.Invoke(e.Message); }
    }

    protected override void ExitThreadCore()
    {
        pauseTimer.Stop(); playlistTimer.Stop(); lockTimer.Stop(); hotkeys.Dispose();
        tray.Visible = false; wall.Stop();
        foreach (var b in bars) b.Close();
        if (S.TaskbarMode == "Custom") Taskbar.Apply("Normal");
        base.ExitThreadCore();
    }
}

/// Settings window: an HTML page (ui.html) in WebView2, talking to C# through JSON messages.
class MainForm : Form
{
    readonly App app;
    readonly WebView2 web = new() { Dock = DockStyle.Fill };
    static readonly Dictionary<string, string> iconCache = new();
    static readonly JsonSerializerOptions camel = new(JsonSerializerDefaults.Web);

    public MainForm(App app)
    {
        this.app = app;
        Text = "Astra Desk"; StartPosition = FormStartPosition.CenterScreen; Icon = AppIcon.Get(new Size(32, 32));
        ClientSize = new Size(1040, 760); MinimumSize = new Size(800, 540);
        BackColor = web.DefaultBackgroundColor = Color.FromArgb(5, 7, 13); // the UI is always dark
        Controls.Add(web);
        HandleCreated += (_, _) => Native.DarkTitleBar(Handle, true);
        Activated += (_, _) => { if (web.CoreWebView2 != null) PushState(); }; // pick up changes made from the tray
        app.LockScreenUpdated += err => { if (err != null) Post(new { notice = "Lock screen sync: " + err, bad = true }); };
        app.Changed += () => { if (Visible && web.CoreWebView2 != null) PushState(); }; // shortcut, playlist, pause changes
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        Init();
    }

    async void Init()
    {
        try
        {
            await web.EnsureCoreWebView2Async(await Web.Env());
            var cw = web.CoreWebView2;
            cw.Settings.AreDefaultContextMenusEnabled = false;
            cw.Settings.IsZoomControlEnabled = false;
            cw.Settings.AreDevToolsEnabled = false;
            // the page is embedded in the exe; unpack it next to the settings so the web view can serve it
            var uiDir = Path.Combine(Settings.Dir, "ui");
            Directory.CreateDirectory(uiDir);
            using (var res = typeof(MainForm).Assembly.GetManifestResourceStream("ui.html"))
            using (var file = File.Create(Path.Combine(uiDir, "ui.html"))) res.CopyTo(file);
            cw.SetVirtualHostNameToFolderMapping("ui.local", uiDir, CoreWebView2HostResourceAccessKind.Allow);
            await cw.AddScriptToExecuteOnDocumentCreatedAsync("window.__errs=[];addEventListener('error',e=>__errs.push(e.message))"); // read by --uitest
            Directory.CreateDirectory(Settings.Dir);
            cw.SetVirtualHostNameToFolderMapping("app.local", Settings.Dir, CoreWebView2HostResourceAccessKind.Allow); // wallpaper.html preview
            // BeginInvoke: file dialogs must not open inside WebView2's event callback
            cw.WebMessageReceived += (_, e) => { var json = e.WebMessageAsJson; BeginInvoke(() => OnMessage(json)); };
            if (UiTestDir != null)
            {
                void Once(object _, CoreWebView2NavigationCompletedEventArgs __) { cw.NavigationCompleted -= Once; RunUiTest(); }
                cw.NavigationCompleted += Once;
            }
            cw.Navigate("https://ui.local/ui.html");
        }
        catch (Exception e) { MessageBox.Show("Could not load the settings page: " + e.Message, "Astra Desk"); }
    }

    void OnMessage(string json)
    {
        var m = JsonDocument.Parse(json).RootElement;
        bool On() => m.GetProperty("on").GetBoolean();
        string Str(string key) => m.GetProperty(key).GetString();
        List<string> Ids(string key) => m.GetProperty(key).EnumerateArray().Select(x => x.GetString()).ToList();
        var s = app.S;
        string error = null;
        try
        {
            switch (Str("cmd"))
            {
                case "taskbar":
                    var mode = Str("mode");
                    if (mode is not ("Normal" or "AutoHide" or "Custom")) throw new ArgumentException("Unknown mode " + mode);
                    s.TaskbarMode = mode; app.ApplyTaskbar(); break;
                case "native": Tweaks.SetTaskbarItem(Str("id"), m.GetProperty("value").GetInt32()); break;
                case "tray": Tweaks.SetTrayIcon(Str("id"), On()); break;
                case "barOpacity": s.BarOpacity = Math.Clamp(m.GetProperty("value").GetInt32(), 0, 100); app.RefreshBar(); break;
                case "zones":
                    s.SetZones(Ids("left"), Ids("center"), Ids("right"));
                    app.RefreshBar(); break;
                case "pinRemove": s.PinnedApps.RemoveAt(m.GetProperty("index").GetInt32()); app.RefreshBar(); break;
                case "pinAdd":
                    using (var d = new OpenFileDialog
                    {
                        DereferenceLinks = false, Multiselect = true, Title = "Pin apps to the custom bar",
                        Filter = "Apps & shortcuts|*.exe;*.lnk;*.url;*.bat|All files|*.*",
                        InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                    })
                        if (d.ShowDialog(this) == DialogResult.OK) { s.PinnedApps.AddRange(d.FileNames); app.RefreshBar(); }
                    break;
                case "wallChoose":
                    using (var d = new OpenFileDialog { Title = "Choose a wallpaper", Filter = "Videos, GIFs & images|*.mp4;*.webm;*.gif;*.png;*.jpg;*.jpeg;*.webp;*.bmp" })
                        if (d.ShowDialog(this) == DialogResult.OK) { s.Wallpaper = d.FileName; s.WallpaperEnabled = true; app.ApplyWallpaper(); }
                    break;
                case "wall": s.WallpaperEnabled = On(); app.ApplyWallpaper(); break;
                case "keys":
                    static string CleanKey(string k) => (k ?? "").Trim() is var t && t.Length <= 200 && !t.Any(char.IsWhiteSpace) ? t : throw new ArgumentException("That doesn't look like an API key.");
                    if (m.TryGetProperty("pexels", out var pk)) s.PexelsKey = CleanKey(pk.GetString());
                    if (m.TryGetProperty("pixabay", out var xk)) s.PixabayKey = CleanKey(xk.GetString());
                    s.Save(); break;
                case "wsSearch": WorkshopSearch(Str("q") ?? "", m.GetProperty("page").GetInt32(), Str("source") ?? "all"); return;
                case "wsGet": WorkshopGet(Str("key")); return;
                case "open": OpenLink(Str("url")); break;
                case "browse": Browse(Str("site")); break;
                case "quit": app.Quit(); return;
                case "lockNow": LockNow(); return;
                case "lockSync":
                    s.LockSync = On(); s.Save();
                    if (s.LockSync) LockNow();
                    break;
                case "perf":
                    if (m.TryGetProperty("fullscreen", out var pf)) s.PauseFullscreen = pf.GetBoolean();
                    if (m.TryGetProperty("battery", out var pb)) s.PauseOnBattery = pb.GetBoolean();
                    if (m.TryGetProperty("maximized", out var pm)) s.PauseMaximized = pm.GetBoolean();
                    s.Save(); app.CheckPause(); break;
                case "pause": app.TogglePause(); break;
                case "next": app.NextWallpaper(); break;
                case "playlist":
                    if (m.TryGetProperty("minutes", out var pmin)) s.PlaylistMinutes = pmin.GetInt32() is var mm && new[] { 0, 5, 15, 30, 60 }.Contains(mm) ? mm : throw new ArgumentException("Unknown interval");
                    if (m.TryGetProperty("shuffle", out var psh)) s.PlaylistShuffle = psh.GetBoolean();
                    s.Save(); app.ApplyPlaylist(); break;
                case "playlistFolder":
                    if (m.TryGetProperty("reset", out _)) s.PlaylistFolder = "";
                    else using (var d = new FolderBrowserDialog { Description = "Folder of wallpapers to rotate through", SelectedPath = app.PlaylistFolder, UseDescriptionForTitle = true })
                        if (d.ShowDialog(this) == DialogResult.OK) s.PlaylistFolder = d.SelectedPath;
                    s.Save(); break;
                case "clockAll": s.ClockAllScreens = On(); app.ApplyWallpaper(); break;
                case "saver":
                    if (m.TryGetProperty("on", out var so)) s.ScreensaverOn = so.GetBoolean();
                    if (m.TryGetProperty("minutes", out var smin)) s.ScreensaverMinutes = Math.Clamp(smin.GetInt32(), 1, 120);
                    if (m.TryGetProperty("lock", out var sl)) s.ScreensaverLock = sl.GetBoolean();
                    s.Save(); Screensaver.Apply(s); break;
                case "saverPreview": Screensaver.Preview(); break;
                case "welcomed": s.Welcomed = true; s.Save(); break;
                case "wallFx":
                    static string Pick(string v, string[] allowed) => allowed.Contains(v) ? v : throw new ArgumentException("Unknown option " + v);
                    if (m.TryGetProperty("motion", out var mo)) s.WallpaperMotion = Pick(mo.GetString(), WallpaperHost.Motions);
                    if (m.TryGetProperty("overlay", out var ov)) s.WallpaperOverlay = Pick(ov.GetString(), WallpaperHost.Overlays);
                    if (m.TryGetProperty("speed", out var sp)) s.WallpaperSpeed = Pick(sp.GetString(), WallpaperHost.Speeds);
                    app.ApplyWallpaper(); break;
                case "clock":
                    if (m.TryGetProperty("style", out var cst)) s.ClockStyle = Pick(cst.GetString(), WallpaperHost.ClockStyles);
                    if (m.TryGetProperty("position", out var cpo)) s.ClockPosition = Pick(cpo.GetString(), WallpaperHost.ClockPositions);
                    if (m.TryGetProperty("size", out var csz)) s.ClockSize = Pick(csz.GetString(), WallpaperHost.ClockSizes);
                    if (m.TryGetProperty("h24", out var c24)) s.Clock24h = c24.GetBoolean();
                    if (m.TryGetProperty("date", out var cdt)) s.ClockDate = cdt.GetBoolean();
                    app.ApplyWallpaper(); break;
                case "tile": Tiler.Apply(Str("mode")); break;
                case "display": Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }); break;
                case "icons": Tweaks.SetDesktopIcons(On()); break;
                case "dark": Tweaks.DarkMode(On()); break;
                case "startup": Tweaks.SetStartWithWindows(On()); break;
            }
        }
        catch (Exception e) { error = e.Message; }
        PushState(error);
    }

    void PushState(string error = null)
    {
        var s = app.S;
        var page = WallpaperHost.PreviewMedia(s); // what wallpaper.html shows (clip, Windows wallpaper, or "" = solid color)
        if (!string.IsNullOrEmpty(page))
        {
            web.CoreWebView2.ClearVirtualHostNameToFolderMapping("media.local");
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("media.local", Path.GetDirectoryName(page), CoreWebView2HostResourceAccessKind.Allow);
        }
        string wallUrl = page == null ? null : "https://media.local/" + Uri.EscapeDataString(Path.GetFileName(page));
        var look = string.Join("|", page, s.WallpaperEnabled, s.WallpaperMotion, s.WallpaperOverlay, s.WallpaperSpeed,
            s.ClockStyle, s.ClockPosition, s.ClockSize, s.Clock24h, s.ClockDate);
        var state = new
        {
            s.TaskbarMode, s.BarOpacity, s.BarLeft, s.BarCenter, s.BarRight, s.WallpaperEnabled,
            elements = Bar.Elements.Select(e => new { id = e.Id, label = e.Label }),
            pinned = s.PinnedApps.Select(p => new { name = Path.GetFileNameWithoutExtension(p), icon = IconData(p) }),
            taskbarItems = Tweaks.ReadTaskbarItems(), tray = Tweaks.TrayIcons(),
            wallpaperName = Path.GetFileName(s.Wallpaper), wallUrl, s.WallpaperMotion, s.WallpaperOverlay, s.WallpaperSpeed,
            // same page as the desktop; the query string only changes when the look changes, so the preview doesn't restart needlessly
            previewUrl = page == null ? null : "https://app.local/wallpaper.html?v=" + look.GetHashCode() + "&main",
            s.ClockStyle, s.ClockPosition, s.ClockSize, s.Clock24h, s.ClockDate, s.LockSync, s.ClockAllScreens,
            s.PauseFullscreen, s.PauseOnBattery, s.PauseMaximized, pauseReason = app.PauseReason, manualPause = app.ManualPause,
            playlistFolder = app.PlaylistFolder, playlistCount = app.PlaylistFiles().Count, s.PlaylistMinutes, s.PlaylistShuffle,
            s.ScreensaverOn, s.ScreensaverMinutes, s.ScreensaverLock, s.Welcomed,
            shortcuts = app.Shortcuts.Select(k => new { keys = k.Keys, action = k.Action, ok = k.Ok }),
            version = Installer.Version, installed = Installer.IsInstalled, screens = Display.All().Count,
            iconsVisible = Tweaks.DesktopIconsVisible, dark = Tweaks.IsDark, startup = Tweaks.StartsWithWindows, error,
            hasPexels = !string.IsNullOrEmpty(s.PexelsKey), hasPixabay = !string.IsNullOrEmpty(s.PixabayKey), // keys never go to the page
        };
        Post(state);
    }

    void Post(object message) => web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message, camel));

    async void WorkshopSearch(string q, int page, string source)
    {
        try
        {
            var (items, errors) = await Workshop.Search(app.S, q.Trim(), Math.Max(1, page), source);
            Post(new { workshop = new { q, page, items, errors } });
        }
        catch (Exception e) { Post(new { workshop = new { q, page, items = Array.Empty<object>(), errors = new[] { e.Message } } }); }
    }

    async void WorkshopGet(string key)
    {
        try
        {
            var file = await Workshop.Download(key, pct => Post(new { download = new { key, pct } }));
            UseWallpaper(file);
            Post(new { download = new { key, pct = 100, done = true } });
        }
        catch (Exception e) { Post(new { download = new { key, error = e.Message } }); }
    }

    /// Sets a downloaded file as the wallpaper; a still image gets a gentle motion so it's animated right away.
    void UseWallpaper(string file) { app.SetWallpaper(file); PushState(); }

    public static string UiTestDir;
    public static bool UiTestDownload;

    /// `--uitest [dir] [--download]`: opens every page, scrolls it, screenshots it, runs a real Workshop search and
    /// collects script errors into dir\report.txt; with --download also installs a clip through the MoeWalls window
    /// (without changing the wallpaper, and deletes it afterwards). Never changes settings. Quits when done.
    async void RunUiTest()
    {
        var report = new List<string>();
        var cw = web.CoreWebView2;
        Task<string> Js(string code) => cw.ExecuteScriptAsync(code);
        async Task Shot(string name)
        {
            await using var f = File.Create(Path.Combine(UiTestDir, name + ".png"));
            await cw.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, f);
        }
        try
        {
            Directory.CreateDirectory(UiTestDir);
            await Task.Delay(1500);
            await Shot("00-start");
            report.Add("welcome overlay shown: " + await Js("!document.getElementById('welcome').hidden"));
            await Js("document.getElementById('welcome').hidden=true");
            int n = 1;
            foreach (var sec in new[] { "taskbar", "custombar", "workshop", "wallpaper", "layout", "desktop" })
            {
                await Js($"document.querySelector('nav [data-go=\"{sec}\"]').click()");
                await Task.Delay(sec == "workshop" ? 9000 : 900); // workshop runs a live search
                report.Add($"{sec}: shown={await Js($"document.getElementById('{sec}').classList.contains('on')")}");
                await Shot($"{n:00}-{sec}-top");
                await Js("document.querySelector('main').scrollTop=1e6"); await Task.Delay(500);
                await Shot($"{n++:00}-{sec}-bottom");
                await Js("document.querySelector('main').scrollTop=0");
            }
            report.Add("workshop results: " + await Js("document.querySelectorAll('#wsGrid .wcard').length"));
            report.Add("workshop status: " + await Js("document.getElementById('wsStatus').textContent"));
            report.Add("shortcuts listed: " + await Js("document.querySelectorAll('#keyList li').length"));
            report.Add("custom bar zone chips: " + await Js("document.querySelectorAll('.zone .chip').length"));

            // drag & drop + slider, done the way the browser sees a mouse drag; your layout and opacity are put back after
            var s = app.S;
            var (left, center, right, opacity) = (s.BarLeft.ToList(), s.BarCenter.ToList(), s.BarRight.ToList(), s.BarOpacity);
            await Js("document.querySelector('nav [data-go=\"custombar\"]').click()"); await Task.Delay(600);
            await Js("""
                (()=>{const chip=document.querySelector('.chip[data-id="clock"]'),zone=document.getElementById('zLeft'),dt=new DataTransfer();
                 chip.dispatchEvent(new DragEvent('dragstart',{bubbles:true,dataTransfer:dt}));
                 const r=zone.getBoundingClientRect();
                 zone.dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true,dataTransfer:dt,clientX:r.right-4,clientY:r.bottom-4}));
                 zone.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));
                 chip.dispatchEvent(new DragEvent('dragend',{bubbles:true,dataTransfer:dt}));})()
                """);
            await Task.Delay(1200);
            report.Add($"drag 'Clock' into the Left zone: saved={s.BarLeft.Contains("clock")} (left zone now: {string.Join(",", s.BarLeft)})");
            await Js("(()=>{const o=document.getElementById('opacity');o.value=77;o.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Task.Delay(1200);
            report.Add($"transparency slider to 77%: saved={s.BarOpacity == 77}");
            s.SetZones(left, center, right); s.BarOpacity = opacity; app.RefreshBar(); PushState();
            report.Add($"layout and opacity restored: {s.BarLeft.SequenceEqual(left) && s.BarOpacity == opacity}");

            // Pexels / Pixabay against their live servers with invalid keys: requests reach them, errors are handled, Wallhaven still answers
            var (items, errors) = await Workshop.Search(new Settings { PexelsKey = "invalid-test-key", PixabayKey = "invalid-test-key" }, "anime", 1, "all");
            report.Add($"stock sources with invalid keys: {string.Join(" | ", errors)}; wallhaven results still shown: {items.Count(i => i.Source == "Wallhaven")}");
            if (UiTestDownload) report.AddRange(await SiteBrowser.TestDownload());
            report.Add("script errors: " + await Js("JSON.stringify(window.__errs)"));
        }
        catch (Exception e) { report.Add("TEST CRASHED: " + e); }
        File.WriteAllLines(Path.Combine(UiTestDir, "report.txt"), report);
        app.Quit();
    }

    async void LockNow()
    {
        try { await app.UpdateLockScreen(); Post(new { notice = "✓ Lock screen updated. Press Win + L to see it." }); }
        catch (Exception e) { Post(new { notice = "Lock screen: " + e.Message, bad = true }); }
    }

    SiteBrowser browser;

    void Browse(string site)
    {
        if (!SiteBrowser.Sites.ContainsKey(site ?? "")) throw new ArgumentException("Unknown site " + site);
        if (browser is { IsDisposed: false }) browser.Go(site); else browser = new SiteBrowser(site, UseWallpaper);
        browser.Show(); browser.Activate();
    }

    /// Credits and "get a key" links open in the real browser, and only for the known sources.
    static void OpenLink(string url)
    {
        string[] allowed = ["pexels.com", "pixabay.com", "wallhaven.cc", "moewalls.com", "motionbgs.com"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" || !allowed.Any(d => SiteBrowser.OnSite(u.Host, d)))
            throw new ArgumentException("Blocked link: " + url);
        Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
    }

    static string IconData(string path)
    {
        if (iconCache.TryGetValue(path, out var cached)) return cached;
        string data = null;
        try
        {
            using var ico = Icon.ExtractAssociatedIcon(path);
            using var bmp = ico.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            data = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch { } // missing file or folder: the page shows a letter instead
        return iconCache[path] = data;
    }
}

/// In-app Workshop: searches free, licensed video libraries (Pexels, Pixabay) and downloads a clip as the wallpaper.
static class Workshop
{
    public record Item(string Key, string Source, string Title, string Thumb, string Preview, string Author, string Page, int Width, int Duration,
        [property: JsonIgnore] string Download, string Kind = "video");

    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Wallpapers");
    static readonly HttpClient http = CreateHttp();
    static readonly Dictionary<string, Item> seen = new(); // the page can only download what we returned to it
    static readonly Dictionary<string, (DateTime At, List<Item> Items)> pixabayCache = new(); // Pixabay asks clients to cache searches for 24 h

    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopStyler/1.0");
        return h;
    }

    public static async Task<(List<Item> Items, List<string> Errors)> Search(Settings s, string q, int page, string source)
    {
        var jobs = new List<(string Name, Task<List<Item>> Task)>();
        var errors = new List<string>();
        bool Want(string name) => source == "all" || source == name;
        if (Want("wallhaven")) jobs.Add(("Wallhaven", Wallhaven(q, page))); // no key needed
        if (Want("pexels"))
            if (!string.IsNullOrEmpty(s.PexelsKey)) jobs.Add(("Pexels", Pexels(s.PexelsKey, q, page)));
            else if (source == "pexels") errors.Add("Pexels needs a free API key: click \"manage API keys\".");
        if (Want("pixabay"))
            if (!string.IsNullOrEmpty(s.PixabayKey)) jobs.Add(("Pixabay", Pixabay(s.PixabayKey, q, page)));
            else if (source == "pixabay") errors.Add("Pixabay needs a free API key: click \"manage API keys\".");
        var lists = new List<List<Item>>();
        foreach (var (name, task) in jobs)
            try { lists.Add(await task); } catch (Exception e) { errors.Add($"{name}: {e.Message}"); } // one source failing still shows the other
        var merged = Interleave(lists);
        foreach (var it in merged) seen[it.Key] = it;
        return (merged, errors);
    }

    public static List<Item> Interleave(List<List<Item>> lists) =>
        Enumerable.Range(0, lists.Count == 0 ? 0 : lists.Max(l => l.Count)).SelectMany(i => lists.Where(l => i < l.Count).Select(l => l[i])).ToList();

    static async Task<List<Item>> Pexels(string key, string q, int page)
    {
        var url = q.Length == 0
            ? $"https://api.pexels.com/videos/popular?per_page=24&page={page}&min_width=1920"
            : $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(q)}&per_page=24&page={page}&orientation=landscape";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", key);
        return ParsePexels(await GetJson(req));
    }

    /// Wallhaven's public API: Anime category only, safe-for-work only, widescreen 1080p+. Still images the app then animates.
    static async Task<List<Item>> Wallhaven(string q, int page)
    {
        var sorting = q.Length == 0 ? "toplist&topRange=1M" : "relevance";
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(q)}&categories=010&purity=100&atleast=1920x1080&ratios=16x9,16x10&sorting={sorting}&page={page}");
        return ParseWallhaven(await GetJson(req), q);
    }

    public static List<Item> ParseWallhaven(JsonElement root, string q)
    {
        var items = new List<Item>();
        if (!root.TryGetProperty("data", out var data)) return items;
        var title = q.Length == 0 ? "Top anime wallpaper" : char.ToUpper(q[0]) + q[1..];
        foreach (var w in data.EnumerateArray())
        {
            var id = Str(w, "id"); var path = Str(w, "path");
            if (id == null || path == null || !Regex.IsMatch(id, "^[a-z0-9]+$")) continue; // id becomes a file name
            var thumb = w.TryGetProperty("thumbs", out var t) ? Str(t, "large") ?? Str(t, "original") : null;
            items.Add(new Item("wallhaven:" + id, "Wallhaven", title, thumb, null, "Wallhaven", Str(w, "url"), Int(w, "dimension_x"), 0, path, "image"));
        }
        return items;
    }

    /// File extension for a download URL; anything unexpected is treated as a video.
    public static string FileExt(string url)
    {
        var ext = Uri.TryCreate(url, UriKind.Absolute, out var u) ? Path.GetExtension(u.AbsolutePath).ToLowerInvariant() : "";
        return ext is ".mp4" or ".webm" or ".jpg" or ".jpeg" or ".png" or ".webp" ? ext : ".mp4";
    }

    static async Task<List<Item>> Pixabay(string key, string q, int page)
    {
        var cacheKey = q + "|" + page;
        if (pixabayCache.TryGetValue(cacheKey, out var hit) && DateTime.UtcNow - hit.At < TimeSpan.FromHours(24)) return hit.Items;
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://pixabay.com/api/videos/?key={Uri.EscapeDataString(key)}&q={Uri.EscapeDataString(q)}&per_page=24&page={page}&safesearch=true&order=popular");
        var items = ParsePixabay(await GetJson(req));
        pixabayCache[cacheKey] = (DateTime.UtcNow, items);
        return items;
    }

    static async Task<JsonElement> GetJson(HttpRequestMessage req)
    {
        using var res = await http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (res.IsSuccessStatusCode && !body.StartsWith("[ERROR")) return JsonDocument.Parse(body).RootElement.Clone();
        throw new Exception((int)res.StatusCode switch
        {
            401 or 403 => "API key rejected. Check it in Connect sources.",
            400 when body.Contains("key", StringComparison.OrdinalIgnoreCase) => "API key rejected. Check it in Connect sources.",
            429 => "too many searches, wait a minute.",
            _ => $"server error {(int)res.StatusCode}.",
        });
    }

    static int Int(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// Smallest file that still covers a 1920px screen (sharp without 4K-sized downloads), else the largest available.
    public static (int W, string Url) Best(List<(int W, string Url)> files) =>
        files.Where(f => f.W >= 1920).OrderBy(f => f.W).Concat(files.OrderByDescending(f => f.W)).FirstOrDefault();

    static (int W, string Url) SmallPreview(List<(int W, string Url)> files) =>
        files.Where(f => f.W >= 480).OrderBy(f => f.W).Concat(files.OrderBy(f => f.W)).FirstOrDefault();

    /// "https://www.pexels.com/video/neon-city-lights-123/" -> "Neon city lights"
    public static string SlugTitle(string pageUrl)
    {
        var slug = Regex.Replace((pageUrl ?? "").TrimEnd('/').Split('/').Last(), @"-?\d+$", "").Replace('-', ' ').Trim();
        return slug.Length == 0 ? "Untitled" : char.ToUpper(slug[0]) + slug[1..];
    }

    public static List<Item> ParsePexels(JsonElement root)
    {
        var items = new List<Item>();
        if (!root.TryGetProperty("videos", out var videos)) return items;
        foreach (var v in videos.EnumerateArray())
        {
            var files = v.TryGetProperty("video_files", out var vf)
                ? vf.EnumerateArray().Where(f => Str(f, "file_type") == "video/mp4" && Int(f, "width") > 0 && Str(f, "link") != null)
                    .Select(f => (W: Int(f, "width"), Url: Str(f, "link"))).ToList()
                : [];
            if (files.Count == 0) continue;
            var best = Best(files);
            var page = Str(v, "url");
            var author = v.TryGetProperty("user", out var u) ? Str(u, "name") : null;
            items.Add(new Item("pexels:" + v.GetProperty("id").GetInt64(), "Pexels", SlugTitle(page), Str(v, "image"), SmallPreview(files).Url,
                author ?? "Unknown", page, best.W, Int(v, "duration"), best.Url));
        }
        return items;
    }

    public static List<Item> ParsePixabay(JsonElement root)
    {
        var items = new List<Item>();
        if (!root.TryGetProperty("hits", out var hits)) return items;
        foreach (var h in hits.EnumerateArray())
        {
            if (!h.TryGetProperty("videos", out var vids)) continue;
            var sizes = new[] { "large", "medium", "small", "tiny" }.Where(n => vids.TryGetProperty(n, out _)).ToDictionary(n => n, n => vids.GetProperty(n));
            var files = sizes.Values.Select(sz => (W: Int(sz, "width"), Url: Str(sz, "url"))).Where(f => !string.IsNullOrEmpty(f.Url) && f.W > 0).ToList();
            if (files.Count == 0) continue;
            var best = Best(files);
            var thumb = new[] { "medium", "small", "large", "tiny" }.Where(sizes.ContainsKey).Select(n => Str(sizes[n], "thumbnail")).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            var tags = Str(h, "tags") ?? "";
            items.Add(new Item("pixabay:" + h.GetProperty("id").GetInt64(), "Pixabay", tags.Length == 0 ? "Untitled" : char.ToUpper(tags[0]) + tags[1..],
                thumb, SmallPreview(files).Url, Str(h, "user") ?? "Unknown", Str(h, "pageURL"), best.W, Int(h, "duration"), best.Url));
        }
        return items;
    }

    /// Downloads to Videos\Wallpapers (via a .part file, so a failed download never leaves a broken video) and returns the path.
    public static async Task<string> Download(string key, Action<int> progress)
    {
        if (key == null || !seen.TryGetValue(key, out var item)) throw new ArgumentException("This result expired. Search again.");
        Directory.CreateDirectory(Folder);
        var file = Path.Combine(Folder, key.Replace(':', '-') + FileExt(item.Download));
        if (File.Exists(file)) return file;
        var tmp = file + ".part";
        try
        {
            using var res = await http.GetAsync(item.Download, HttpCompletionOption.ResponseHeadersRead);
            res.EnsureSuccessStatusCode();
            long total = res.Content.Headers.ContentLength ?? 0, done = 0;
            int last = -100;
            await using (var src = await res.Content.ReadAsStreamAsync())
            await using (var dst = File.Create(tmp))
            {
                var buf = new byte[1 << 16]; int n;
                while ((n = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n)); done += n;
                    int pct = total > 0 ? (int)(done * 100 / total) : -1;
                    if (pct - last >= 5 || (pct < 0 && last != -1)) { last = pct; progress(pct); }
                }
            }
            File.Move(tmp, file, true);
            return file;
        }
        catch { File.Delete(tmp); throw; }
    }
}

/// MoeWalls / MotionBgs inside the app. You browse the site normally (its ads and pages as usual) and click its own Download
/// button; instead of a browser download, the clip is saved to Videos\Wallpapers and set as the wallpaper.
class SiteBrowser : Form
{
    public static readonly Dictionary<string, (string Name, string Url)> Sites = new()
    {
        ["moewalls"] = ("MoeWalls", "https://moewalls.com/"),
        ["motionbgs"] = ("MotionBgs", "https://motionbgs.com/"),
    };

    public static bool OnSite(string host, string domain) => host == domain || host.EndsWith("." + domain);
    public static bool IsMediaFile(string name) =>
        Path.GetExtension(name ?? "").ToLowerInvariant() is ".mp4" or ".webm" or ".mov" or ".mkv" or ".gif" or ".jpg" or ".jpeg" or ".png" or ".webp";

    readonly WebView2 web = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(12, 7, 12, 0), ForeColor = Color.FromArgb(34, 228, 255), BackColor = Color.FromArgb(8, 12, 24), Font = new Font("Cascadia Mono", 9f) };
    readonly Action<string> onWallpaper;

    public SiteBrowser(string site, Action<string> onWallpaper)
    {
        this.onWallpaper = onWallpaper;
        StartPosition = FormStartPosition.CenterScreen; Size = new Size(1300, 880); Icon = AppIcon.Get(new Size(32, 32));
        BackColor = web.DefaultBackgroundColor = Color.FromArgb(5, 7, 13);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 0), BackColor = Color.FromArgb(8, 12, 24) };
        Button B(string text, Action click)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 30, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(230, 241, 255), BackColor = Color.FromArgb(16, 24, 42), Margin = new Padding(0, 0, 6, 0), Font = new Font("Segoe UI", 9.5f) };
            b.FlatAppearance.BorderColor = Color.FromArgb(40, 80, 110);
            b.Click += (_, _) => click();
            bar.Controls.Add(b);
            return b;
        }
        B("◀  Back", () => { if (web.CanGoBack) web.GoBack(); });
        B("Forward  ▶", () => { if (web.CanGoForward) web.GoForward(); });
        B("⟳  Reload", () => web.Reload());
        foreach (var (key, s) in Sites) B(s.Name, () => Go(key));
        Controls.Add(web); Controls.Add(bar); Controls.Add(status);
        HandleCreated += (_, _) => Native.DarkTitleBar(Handle, true);
        Status("Find a wallpaper and click the site's Download button: it installs on your desktop automatically.");
        Init(site);
    }

    void Status(string text) { if (!IsDisposed) status.Text = text; }

    /// Test pass for --uitest --download: opens MoeWalls, goes to the first wallpaper page, clicks its download button
    /// and waits for the clip to install. Uses a test callback (wallpaper unchanged) and deletes the file afterwards.
    public static async Task<List<string>> TestDownload()
    {
        var log = new List<string>();
        var done = new TaskCompletionSource<string>();
        var b = new SiteBrowser("moewalls", file => done.TrySetResult(file));
        b.Show();
        try
        {
            async Task<string> Js(string code) => System.Text.Json.JsonSerializer.Deserialize<string>(await b.web.CoreWebView2.ExecuteScriptAsync(code)) ?? "";
            for (int i = 0; i < 60 && b.web.CoreWebView2 == null; i++) await Task.Delay(500);
            await Task.Delay(8000); // home page, possibly a security check
            // wallpaper pages live under /anime/... (the home page also links guides, which offer .zip tools)
            var post = await Js("(document.querySelector('a[href*=\"moewalls.com/anime/\"][href$=\"-live-wallpaper/\"]')||{}).href||''");
            log.Add("moewalls: first wallpaper page = " + (post == "" ? "(none found)" : post));
            if (post == "") return log;
            b.web.CoreWebView2.Navigate(post);
            await Task.Delay(9000);
            var clicked = await Js("(()=>{const el=document.getElementById('moe-download')||[...document.querySelectorAll('a,button')].find(e=>/download/i.test(e.id+' '+e.className+' '+e.textContent));if(!el)return '';el.click();return (el.id||el.className||el.textContent).toString().trim().slice(0,40);})()");
            log.Add("moewalls: download button = " + (clicked == "" ? "(none found)" : clicked));
            await Task.Delay(6000);
            if (MainForm.UiTestDir != null)
                await using (var f = File.Create(Path.Combine(MainForm.UiTestDir, "moewalls-after-click.png")))
                    await b.web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, f);
            var finished = await Task.WhenAny(done.Task, Task.Delay(120_000));
            if (finished == done.Task)
            {
                var file = done.Task.Result;
                log.Add($"moewalls: installed {Path.GetFileName(file)} ({new FileInfo(file).Length / 1048576.0:0.0} MB), media file = {IsMediaFile(file)}");
                File.Delete(file);
            }
            else log.Add("moewalls: no download within 2 minutes; status line: " + b.status.Text);
        }
        catch (Exception e) { log.Add("moewalls: test failed: " + e.Message); }
        finally { b.Close(); }
        return log;
    }

    public void Go(string site)
    {
        Text = "Astra Desk · " + Sites[site].Name;
        if (web.CoreWebView2 != null) web.CoreWebView2.Navigate(Sites[site].Url);
    }

    async void Init(string site)
    {
        Text = "Astra Desk · " + Sites[site].Name;
        try
        {
            await web.EnsureCoreWebView2Async(await Web.Env());
            var cw = web.CoreWebView2;
            cw.NewWindowRequested += (_, e) => // ads open pop-ups; site links stay in this window, everything else is blocked
            {
                e.Handled = true;
                bool own = Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && Sites.Values.Any(s => OnSite(u.Host, new Uri(s.Url).Host));
                WallpaperHost.Log($"site browser: new window {(own ? "opened here" : "blocked")}: {e.Uri}");
                if (own) cw.Navigate(e.Uri); else Status("Blocked a pop-up.");
            };
            cw.NavigationStarting += (_, e) => WallpaperHost.Log("site browser: navigate " + e.Uri);
            cw.DownloadStarting += OnDownload;
            cw.Navigate(Sites[site].Url);
        }
        catch (Exception e) { Status("Could not open the site: " + e.Message); }
    }

    void OnDownload(object sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var name = Path.GetFileName(e.ResultFilePath);
        WallpaperHost.Log($"site browser: download {name} from {e.DownloadOperation.Uri}");
        if (!IsMediaFile(name)) { e.Cancel = true; Status($"Blocked \"{name}\": only videos and images can become wallpapers."); return; }
        Directory.CreateDirectory(Workshop.Folder);
        var target = Path.Combine(Workshop.Folder, name);
        for (int i = 2; File.Exists(target); i++) target = Path.Combine(Workshop.Folder, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
        e.ResultFilePath = target;
        e.Handled = true; // no browser download bubble; progress shows in our status line
        var op = e.DownloadOperation;
        op.BytesReceivedChanged += (_, _) =>
        {
            long total = Convert.ToInt64(op.TotalBytesToReceive ?? 0);
            Status($"Downloading {name}… " + (total > 0 ? $"{op.BytesReceived * 100 / total}%" : $"{op.BytesReceived / 1048576} MB"));
        };
        op.StateChanged += (_, _) =>
        {
            if (op.State == CoreWebView2DownloadState.Completed) { Status($"✓ {name} is now your wallpaper. Press Win + D to see it."); onWallpaper(op.ResultFilePath); }
            else if (op.State == CoreWebView2DownloadState.Interrupted) Status($"Download failed ({op.InterruptReason}). Try again.");
        };
    }
}

static class Web
{
    static Task<CoreWebView2Environment> env;
    public static Task<CoreWebView2Environment> Env() => env ??= CoreWebView2Environment.CreateAsync(null, Path.Combine(Settings.Dir, "webview"));
}

/// Our own taskbar. A layered window so the background can be fully transparent, registered as an
/// app bar so maximized windows stop above it like they do for the real one.
class Bar : Form
{
    public static readonly (string Id, string Label)[] Elements =
    [
        ("start", "Start"), ("search", "Search"), ("taskview", "Task view"), ("pinned", "Pinned apps"), ("apps", "Open apps"),
        ("desktop", "Show desktop"), ("icons", "Desktop icons"), ("settings", "Styler settings"), ("network", "Network"), ("volume", "Volume"), ("battery", "Battery"), ("clock", "Clock"), ("notifications", "Notifications"),
    ];

    const int WM_BAR = 0x8000 + 0x77; // our app-bar callback message
    const byte WIN = 0x5B;
    static readonly Color Cyan = Color.FromArgb(34, 228, 255), Violet = Color.FromArgb(166, 77, 255);
    static readonly Graphics measure = Graphics.FromImage(new Bitmap(1, 1));
    readonly Settings s;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 400 };
    readonly ToolTip tip = new();
    readonly Dictionary<IntPtr, Bitmap> winIcons = new();
    readonly Dictionary<string, Bitmap> fileIcons = new();
    readonly List<IntPtr> windows = new(); // keeps app buttons in a stable order
    List<Item> items = new();
    string hover;
    float k = 1; // DPI scale
    Font glyphs, textFont;

    record Item(Rectangle R, Action<Graphics, Rectangle> Draw, Action Click, string Tip, string Key);
    public Action OpenSettings;

    readonly Display screen;

    public Bar(Settings s, Display screen)
    {
        this.s = s; this.screen = screen;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        Location = screen.Bounds.Location; // created on its own monitor, so it gets that monitor's DPI
        timer.Tick += (_, _) => { Taskbar.KeepHidden(); if (++ticks % 5 == 0) KeepReserved(); Render(); }; // Explorer re-shows its bar on some events
    }

    static readonly int WM_TASKBARCREATED = Native.RegisterWindowMessage("TaskbarCreated"); // Explorer restarted

    /// Center zone stays centered but never overlaps the left or right zone (it hugs the left one when too wide).
    public static int CenterStart(int width, int leftEnd, int rightStart, int centerWidth) =>
        Math.Max(leftEnd, Math.Min((width - centerWidth) / 2, rightStart - centerWidth));

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000 | 0x8; return cp; } } // layered, toolwindow, noactivate, topmost

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        k = DeviceDpi / 96f;
        glyphs = new Font("Segoe Fluent Icons", 16 * k, GraphicsUnit.Pixel);
        textFont = new Font("Segoe UI", 12 * k, GraphicsUnit.Pixel);
        if (!Display.Testing) { var abd = AppBar(); abd.uCallbackMessage = WM_BAR; Native.SHAppBarMessage(0, ref abd); } // ABM_NEW
        Reserve();
        timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Dispose();
        if (!Display.Testing) { var abd = AppBar(); Native.SHAppBarMessage(1, ref abd); } // ABM_REMOVE: give the screen space back
        base.OnFormClosed(e);
    }

    Native.APPBARDATA AppBar() => new() { cbSize = Marshal.SizeOf<Native.APPBARDATA>(), hWnd = Handle };

    int ticks;

    /// Self-healing: if Windows no longer keeps the bar's space free (e.g. Explorer restarted and lost the app bar, or
    /// reset the work area after we re-registered), register and reserve it again. Checked every ~2 s.
    void KeepReserved()
    {
        if (Display.Testing || !IsHandleCreated) return;
        var work = Screen.FromRectangle(screen.Bounds).WorkingArea;
        if (work.Bottom <= Top) return; // still reserved
        WallpaperHost.Log($"custom bar: space lost (work area bottom {work.Bottom}, bar top {Top}), reserving again");
        var abd = AppBar(); abd.uCallbackMessage = WM_BAR;
        Native.SHAppBarMessage(0, ref abd); // ABM_NEW (no-op if Windows still knows the bar)
        Reserve();
    }

    void Reserve()
    {
        var scr = screen.Bounds; int h = (int)(48 * k);
        // fake test screens share one real monitor: Windows would stack their app bars, so they're only positioned
        if (Display.Testing) { Bounds = new Rectangle(scr.Left, scr.Bottom - h, scr.Width, h); Render(); return; }
        var abd = AppBar(); abd.uEdge = 3; // bottom
        abd.rc = new() { L = scr.Left, T = scr.Bottom - h, R = scr.Right, B = scr.Bottom };
        Native.SHAppBarMessage(2, ref abd); // QUERYPOS
        abd.rc.T = abd.rc.B - h;
        Native.SHAppBarMessage(3, ref abd); // SETPOS
        Bounds = new Rectangle(abd.rc.L, abd.rc.T, abd.rc.R - abd.rc.L, h);
        Render();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_TASKBARCREATED) // the new Explorer forgot our app bar and shows its own taskbar again
        {
            var abd = AppBar(); abd.uCallbackMessage = WM_BAR;
            Native.SHAppBarMessage(0, ref abd);
            Taskbar.Apply("Hidden");
            Reserve();
        }
        if (m.Msg == WM_BAR && m.WParam == 1) Reserve(); // ABN_POSCHANGED
        if (m.Msg == WM_BAR && m.WParam == 2) // ABN_FULLSCREENAPP: step behind games and full-screen video
            Native.SetWindowPos(Handle, m.LParam != 0 ? -2 : -1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        base.WndProc(ref m);
    }

    public void Render()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
        int pad = (int)(8 * k);
        var left = Zone(s.BarLeft, _ => pad);
        var right = Zone(s.BarRight, w => Width - pad - w);
        int leftEnd = left.Count > 0 ? left[^1].R.Right : pad, rightStart = right.Count > 0 ? right[0].R.Left : Width - pad;
        items = [.. left, .. Zone(s.BarCenter, w => CenterStart(Width, leftEnd, rightStart, w)), .. right];
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.FromArgb(Math.Max(1, s.BarOpacity * 255 / 100), 5, 7, 13)); // alpha >= 1 keeps empty areas clickable
            if (s.BarOpacity > 0) { using var edge = new Pen(Color.FromArgb(s.BarOpacity * 120 / 100, Cyan)); g.DrawLine(edge, 0, 0, Width, 0); }
            foreach (var it in items)
            {
                if (it.Key == hover)
                {
                    using var hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                    using var p = Round(RectangleF.Inflate(it.R, -3 * k, -5 * k), 6 * k);
                    g.FillPath(hb, p);
                }
                it.Draw(g, it.R);
            }
        }
        Native.PushLayered(Handle, bmp, Left, Top);
    }

    List<Item> Zone(List<string> ids, Func<int, int> start)
    {
        var list = ids.SelectMany(Make).ToList();
        int x = start(list.Sum(i => i.R.Width));
        for (int i = 0; i < list.Count; i++) { list[i] = list[i] with { R = new Rectangle(x, 0, list[i].R.Width, Height) }; x += list[i].R.Width; }
        return list;
    }

    Item It(int w, Action<Graphics, Rectangle> draw, Action click, string tipText, string key) => new(new Rectangle(0, 0, w, Height), draw, click, tipText, key);
    Item Glyph(string glyph, string tipText, Action click, string key) => It((int)(44 * k), (g, r) => DrawText(g, glyph, glyphs, r), click, tipText, key);
    Item TextItem(string t, string tipText, Action click, string key) =>
        It((int)Math.Ceiling(measure.MeasureString(t, textFont).Width + 20 * k), (g, r) => DrawText(g, t, textFont, r), click, tipText, key);

    IEnumerable<Item> Make(string id)
    {
        int b = (int)(44 * k);
        switch (id)
        {
            case "start": yield return It(b, DrawLogo, () => Press(WIN), "Start", id); break;
            case "search": yield return Glyph("", "Search", () => Press(WIN, 0x53), id); break;
            case "taskview": yield return Glyph("", "Task view", () => Press(WIN, 0x09), id); break;
            case "network": yield return Glyph("", "Network & quick settings", () => Press(WIN, 0x41), id); break;
            case "volume": yield return Glyph("", "Volume (scroll to change)", () => Press(WIN, 0x41), id); break;
            case "notifications": yield return Glyph("", "Notifications", () => Press(WIN, 0x4E), id); break;
            case "settings": yield return Glyph("", "Astra Desk settings", () => OpenSettings?.Invoke(), id); break;
            case "icons": // grid glyph, dimmed while the icons are hidden
                bool shown = Tweaks.DesktopIconsVisible;
                yield return It(b, (g, r) => DrawText(g, "", glyphs, r, shown ? 255 : 110),
                    () => Tweaks.SetDesktopIcons(!Tweaks.DesktopIconsVisible), shown ? "Hide desktop icons" : "Show desktop icons", id);
                break;
            case "desktop":
                yield return It((int)(14 * k), (g, r) =>
                {
                    using var p = new Pen(Color.FromArgb(110, 255, 255, 255), k);
                    g.DrawLine(p, r.X + r.Width / 2, r.Y + r.Height / 4, r.X + r.Width / 2, r.Bottom - r.Height / 4);
                }, () => Press(WIN, 0x44), "Show desktop", id);
                break;
            case "battery":
                var ps = SystemInformation.PowerStatus;
                if (ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery)) break;
                yield return TextItem($"{ps.BatteryLifePercent * 100:0}%" + (ps.PowerLineStatus == PowerLineStatus.Online ? " ⚡" : ""), "Battery", () => Press(WIN, 0x41), id);
                break;
            case "clock":
                var now = DateTime.Now;
                yield return TextItem(now.ToString("t") + "\n" + now.ToString("d"), now.ToString("D"), () => Press(WIN, 0x4E), id);
                break;
            case "pinned":
                foreach (var path in s.PinnedApps)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    yield return It(b, (g, r) => DrawIcon(g, FileIcon(path), r, name), () => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }), name, "p" + path);
                }
                break;
            case "apps":
                var open = Tiler.AppWindows(includeMinimized: true).Where(w => Display.Of(w).Name == screen.Name).ToList(); // each screen's bar shows its own windows
                windows.RemoveAll(w => !open.Contains(w));
                windows.AddRange(open.Where(w => !windows.Contains(w)));
                foreach (var gone in winIcons.Keys.Where(w => !open.Contains(w)).ToList()) winIcons.Remove(gone);
                var fg = Native.GetForegroundWindow();
                foreach (var w in windows.ToList())
                {
                    var title = Title(w);
                    yield return It(b, (g, r) => { DrawIcon(g, WinIcon(w), r, title); Indicator(g, r, w == fg); }, () => Activate(w), title, "w" + w);
                }
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var it = items.FirstOrDefault(i => i.R.Contains(e.Location));
        if (it?.Key == hover) return;
        hover = it?.Key; Render();
        if (it != null) tip.Show(it.Tip, this, it.R.X, -(int)(34 * k), 2500); else tip.Hide(this);
    }

    protected override void OnMouseLeave(EventArgs e) { hover = null; tip.Hide(this); Render(); base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); // right-click opens the tray menu via ContextMenuStrip
        if (e.Button != MouseButtons.Left) return;
        try { items.FirstOrDefault(i => i.R.Contains(e.Location))?.Click(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Astra Desk"); }
    }

    int wheel; // wheel movement not yet turned into a volume step

    /// One volume step per full wheel notch (120). High-resolution wheels, touchpads and smooth-scrolling drivers send
    /// many small deltas; treating each as a step made the volume jump wildly.
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // where the wheel turned (its own position: the "pointer moved here" message can arrive after the wheel message)
        var under = items.FirstOrDefault(i => i.R.Contains(PointToClient(Cursor.Position)))?.Key;
        if (under != "volume") { wheel = 0; WallpaperHost.Log($"custom bar: wheel {e.Delta:+#;-#} ignored (pointer on '{under ?? "nothing"}')"); return; }
        foreach (var up in WheelSteps(ref wheel, e.Delta))
        {
            Press(up ? (byte)0xAF : (byte)0xAE); // VK_VOLUME_UP / VK_VOLUME_DOWN: Windows changes the volume and shows its flyout
            WallpaperHost.Log($"custom bar: volume {(up ? "up" : "down")} (wheel {e.Delta:+#;-#})");
        }
    }

    /// Adds a wheel delta to the running total and returns the full notches it completes (true = up).
    public static List<bool> WheelSteps(ref int total, int delta)
    {
        if (Math.Sign(delta) != Math.Sign(total)) total = 0; // changing direction starts fresh
        total += delta;
        var steps = new List<bool>();
        while (Math.Abs(total) >= 120) { steps.Add(total > 0); total -= Math.Sign(total) * 120; }
        return steps;
    }

    static void Press(params byte[] keys)
    {
        foreach (var vk in keys) Native.keybd_event(vk, 0, 0, 0);
        foreach (var vk in keys.Reverse()) Native.keybd_event(vk, 0, 2, 0); // KEYEVENTF_KEYUP
    }

    static void Activate(IntPtr h)
    {
        if (Native.GetForegroundWindow() == h && !Native.IsIconic(h)) { Native.ShowWindow(h, 6); return; } // click on the active app minimizes it
        if (Native.IsIconic(h)) Native.ShowWindow(h, 9);
        Native.SetForegroundWindow(h);
    }

    static string Title(IntPtr h)
    {
        var sb = new StringBuilder(Native.GetWindowTextLength(h) + 1);
        Native.GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    Bitmap WinIcon(IntPtr h)
    {
        if (winIcons.TryGetValue(h, out var cached)) return cached;
        IntPtr hi = 0;
        foreach (var size in new[] { 1, 2, 0 }) // WM_GETICON: big, small2, small
            if (Native.SendMessageTimeout(h, 0x7F, size, 0, 2, 50, out hi) != 0 && hi != 0) break;
        if (hi == 0) hi = Native.GetClassLongPtr(h, -14); // GCLP_HICON
        Bitmap bmp = null;
        try { bmp = hi != 0 ? Icon.FromHandle(hi).ToBitmap() : FileIcon(ExePath(h)); } catch { }
        return winIcons[h] = bmp;
    }

    Bitmap FileIcon(string path)
    {
        if (path == null) return null;
        if (fileIcons.TryGetValue(path, out var cached)) return cached;
        Bitmap bmp = null;
        try { bmp = Icon.ExtractAssociatedIcon(path)?.ToBitmap(); } catch { }
        return fileIcons[path] = bmp;
    }

    static string ExePath(IntPtr h)
    {
        Native.GetWindowThreadProcessId(h, out uint pid);
        try { return Process.GetProcessById((int)pid).MainModule?.FileName; } catch { return null; } // elevated apps refuse
    }

    void DrawText(Graphics g, string t, Font f, Rectangle r, int alpha = 255)
    {
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var shadow = new SolidBrush(Color.FromArgb(150 * alpha / 255, 0, 0, 0)); // keeps text readable on a transparent bar
        using var fill = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
        var sr = r; sr.Offset(0, (int)Math.Max(1, k));
        g.DrawString(t, f, shadow, sr, sf);
        g.DrawString(t, f, fill, r, sf);
    }

    void DrawLogo(Graphics g, Rectangle r)
    {
        float sq = 8 * k, gap = 1.5f * k, total = sq * 2 + gap, x = r.X + (r.Width - total) / 2, y = r.Y + (r.Height - total) / 2;
        using var br = new LinearGradientBrush(new RectangleF(x, y, total, total), Cyan, Violet, 45f);
        g.FillRectangle(br, x, y, sq, sq); g.FillRectangle(br, x + sq + gap, y, sq, sq);
        g.FillRectangle(br, x, y + sq + gap, sq, sq); g.FillRectangle(br, x + sq + gap, y + sq + gap, sq, sq);
    }

    void DrawIcon(Graphics g, Bitmap icon, Rectangle r, string name)
    {
        float size = 24 * k;
        var dst = new RectangleF(r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size);
        if (icon != null) { g.DrawImage(icon, dst); return; }
        using var br = new LinearGradientBrush(dst, Cyan, Violet, 45f);
        using var p = Round(dst, 6 * k);
        g.FillPath(br, p);
        DrawText(g, string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpper(), textFont, Rectangle.Round(dst));
    }

    void Indicator(Graphics g, Rectangle r, bool active)
    {
        float w = (active ? 16 : 6) * k, h = 3 * k;
        using var br = new SolidBrush(active ? Cyan : Color.FromArgb(150, 255, 255, 255));
        using var p = Round(new RectangleF(r.X + (r.Width - w) / 2, r.Bottom - 6 * k, w, h), h / 2);
        g.FillPath(br, p);
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

static class Taskbar
{
    static List<IntPtr> Bars()
    {
        var bars = new List<IntPtr>();
        var main = Native.FindWindow("Shell_TrayWnd", null);
        if (main != 0) bars.Add(main);
        for (IntPtr h = 0; (h = Native.FindWindowEx(0, h, "Shell_SecondaryTrayWnd", null)) != 0;) bars.Add(h);
        return bars;
    }

    public static void Apply(string mode)
    {
        var abd = new Native.APPBARDATA { cbSize = Marshal.SizeOf<Native.APPBARDATA>(), hWnd = Native.FindWindow("Shell_TrayWnd", null), lParam = mode == "Normal" ? 2 : 1 };
        Native.SHAppBarMessage(0x0A, ref abd); // ABM_SETSTATE: auto-hide frees the screen space even in Hidden mode
        foreach (var h in Bars()) Native.ShowWindow(h, mode == "Hidden" ? 0 : 8);
    }

    public static void Unhide() { foreach (var h in Bars()) Native.ShowWindow(h, 8); }

    public static void KeepHidden() { foreach (var h in Bars()) if (Native.IsWindowVisible(h)) Native.ShowWindow(h, 0); }
}

class WallpaperHost
{
    readonly List<Form> forms = new();

    class WallForm : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
    }

    public static readonly string[] Motions = ["none", "zoom", "pan", "drift", "sway", "pulse", "hue"];
    public static readonly string[] Overlays = ["none", "snow", "rain", "sakura", "embers", "stars", "scanlines", "glitch"];
    public static readonly string[] Speeds = ["slow", "normal", "fast"];

    /// The wallpaper page: the media plus an optional CSS motion and a canvas/CSS overlay.
    public static readonly string[] ClockStyles = ["off", "neon", "minimal", "digital", "analog", "glass"];
    public static readonly string[] ClockPositions = ["center", "top-center", "top-left", "top-right", "bottom-left", "bottom-right"];
    public static readonly string[] ClockSizes = ["small", "medium", "large"];

    /// What the desktop layer shows: the animated file while playing; else the Windows wallpaper ("" = solid color) when the clock
    /// needs the layer; null = nothing, so Windows draws its own wallpaper.
    public static string MediaFor(Settings s) =>
        s.WallpaperEnabled && File.Exists(s.Wallpaper) ? s.Wallpaper : s.ClockStyle is not (null or "off") ? Tweaks.StillWallpaper() : null;

    /// What the settings preview shows: the desktop layer, or the chosen clip (with its animation) while paused.
    public static string PreviewMedia(Settings s) => MediaFor(s) ?? (File.Exists(s.Wallpaper) ? s.Wallpaper : null);

    public static string Html(Settings s, string media, bool animate)
    {
        string motion = animate ? s.WallpaperMotion : "none", overlay = animate ? s.WallpaperOverlay : "none", speed = s.WallpaperSpeed;
        double k = speed switch { "slow" => 1.8, "fast" => 0.5, _ => 1 }; // duration multiplier
        string Secs(double s) => (s * k).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s"; // "21.6s", never "21,6s"
        var src = "https://media.local/" + Uri.EscapeDataString(Path.GetFileName(media));
        var el = string.IsNullOrEmpty(media) ? "" : Path.GetExtension(media).ToLowerInvariant() is ".mp4" or ".webm" or ".mov" or ".mkv"
            ? $"<video id=m src='{src}' autoplay loop muted playsinline></video>"
            : $"<img id=m src='{src}'>";
        var (frames, secs, timing) = motion switch
        {
            "zoom" => ("from{transform:scale(1)}to{transform:scale(1.15)}", 30, "ease-in-out infinite alternate"),
            "pan" => ("from{transform:scale(1.2) translateX(-7%)}to{transform:scale(1.2) translateX(7%)}", 40, "ease-in-out infinite alternate"),
            "drift" => ("0%,100%{transform:scale(1.12) translate(0,0)}25%{transform:scale(1.12) translate(-2.5%,1.5%)}50%{transform:scale(1.12) translate(1.5%,-2.5%)}75%{transform:scale(1.12) translate(2.5%,2%)}", 60, "ease-in-out infinite"),
            "sway" => ("from{transform:scale(1.1) rotate(-1.2deg)}to{transform:scale(1.1) rotate(1.2deg)}", 12, "ease-in-out infinite alternate"),
            "pulse" => ("0%,100%{filter:brightness(1) saturate(1)}50%{filter:brightness(1.18) saturate(1.35)}", 6, "ease-in-out infinite"),
            "hue" => ("from{filter:hue-rotate(0deg)}to{filter:hue-rotate(360deg)}", 40, "linear infinite"),
            _ => ("", 0, ""),
        };
        var css = $"body{{margin:0;background:{(string.IsNullOrEmpty(media) ? Tweaks.DesktopColor() : "#000")};overflow:hidden}}"
            + "#m{position:fixed;inset:0;width:100vw;height:100vh;object-fit:cover}"
            + "#fx{position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none}"
            + (frames == "" ? "" : $"@keyframes fx{{{frames}}}#m{{animation:fx {Secs(secs)} {timing}}}");
        var layer = overlay switch
        {
            "scanlines" => $"<div id=fx style='background:repeating-linear-gradient(0deg,rgba(0,0,0,.28) 0 1px,transparent 1px 3px);animation:fl {Secs(5)} infinite'></div>"
                         + "<style>@keyframes fl{0%,100%{opacity:.9}50%{opacity:.6}52%{opacity:.95}}</style>",
            "glitch" => "<canvas id=fx></canvas><script>" + Glitch.Replace("__SPEED__", k.ToString(System.Globalization.CultureInfo.InvariantCulture)) + "</script>",
            "snow" or "rain" or "sakura" or "embers" or "stars" => "<canvas id=fx></canvas><script>" + Particles.Replace("__KIND__", overlay)
                .Replace("__SPEED__", k.ToString(System.Globalization.CultureInfo.InvariantCulture)) + "</script>",
            _ => "",
        };
        css += ".paused,.paused *{animation-play-state:paused!important}"; // pause freezes every CSS animation
        return $"<!doctype html><meta charset=utf-8><style>{css}</style>{PauseScript}<body>{el}{layer}{ClockHtml(s)}</body>";
    }

    /// The app calls setPaused(true) during games / full screen / battery: video stops, canvas loops idle (see Particles, Glitch).
    const string PauseScript = "<script>window.paused=false;window.setPaused=p=>{window.paused=p;"
        + "document.querySelectorAll('video').forEach(v=>p?v.pause():v.play().catch(()=>{}));document.documentElement.classList.toggle('paused',p);};</script>";

    static string ClockHtml(Settings s)
    {
        if (s.ClockStyle is null or "off" || !ClockStyles.Contains(s.ClockStyle)) return "";
        double scale = s.ClockSize switch { "small" => 0.7, "large" => 1.45, _ => 1 };
        return ClockTemplate.Replace("__STYLE__", s.ClockStyle)
            .Replace("__POS__", ClockPositions.Contains(s.ClockPosition) ? s.ClockPosition : "center")
            .Replace("__SCALE__", scale.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("__H24__", s.Clock24h ? "true" : "false").Replace("__DATE__", s.ClockDate ? "true" : "false")
            .Replace("__LOCALE__", Locale());
    }

    /// Windows' regional format (e.g. "fr-FR"), so the date reads in the user's language.
    public static string Locale()
    {
        var name = System.Globalization.CultureInfo.CurrentCulture.Name;
        return Regex.IsMatch(name, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$") ? name : "en-US";
    }

    const string ClockTemplate = """
        <style>
        #clock{position:fixed;z-index:5;font-size:calc(.834vw*__SCALE__);color:#fff;pointer-events:none;text-align:center}
        #clock.center{left:50%;top:50%;transform:translate(-50%,-50%)}#clock.top-center{left:50%;top:6vh;transform:translateX(-50%)}
        #clock.top-left{left:4vw;top:6vh;text-align:left}#clock.top-right{right:4vw;top:6vh;text-align:right}
        #clock.bottom-left{left:4vw;bottom:13vh;text-align:left}#clock.bottom-right{right:4vw;bottom:13vh;text-align:right}
        #clock .t{line-height:1;white-space:nowrap}#clock .d{margin-top:.6em}#clock .x{font-size:.3em;margin-left:.2em;opacity:.75;vertical-align:top}
        .neon .t{font:600 7em 'Cascadia Mono',Consolas,monospace;letter-spacing:.04em;color:#e9fdff;text-shadow:0 0 .08em #fff,0 0 .25em #22e4ff,0 0 .6em #22e4ff,0 0 1.2em #a64dff}
        .neon .d{font:1.3em 'Cascadia Mono',Consolas,monospace;letter-spacing:.35em;text-transform:uppercase;color:#9fefff;text-shadow:0 0 .5em #22e4ff}
        .minimal .t{font:200 8em 'Segoe UI Variable Display','Segoe UI',sans-serif;letter-spacing:-.02em;text-shadow:0 2px 20px rgba(0,0,0,.35)}
        .minimal .d{font:300 1.6em 'Segoe UI Variable Display','Segoe UI',sans-serif;text-shadow:0 1px 10px rgba(0,0,0,.45)}
        .digital .t{position:relative;font:700 6.5em 'Cascadia Mono',Consolas,monospace;color:#ffb000;text-shadow:0 0 .2em rgba(255,176,0,.7);
          padding:.15em .3em;background:rgba(10,6,0,.55);border:1px solid rgba(255,176,0,.35);border-radius:.12em}
        .digital .t::before{content:attr(data-ghost);position:absolute;left:.3em;top:.15em;color:rgba(255,176,0,.09);text-shadow:none}
        .digital .d{font:700 1.2em 'Cascadia Mono',Consolas,monospace;color:#ffb000;letter-spacing:.3em;text-transform:uppercase;opacity:.85}
        .glass{padding:1.6em 2.4em;border-radius:1.4em;background:rgba(255,255,255,.12);backdrop-filter:blur(18px) saturate(1.4);
          border:1px solid rgba(255,255,255,.25);box-shadow:0 10px 40px rgba(0,0,0,.25)}
        .glass .t{font:600 5.5em 'Segoe UI Variable Display','Segoe UI',sans-serif}.glass .d{font:500 1.3em 'Segoe UI Variable Text','Segoe UI',sans-serif;opacity:.85}
        .analog svg{width:16em;height:16em;filter:drop-shadow(0 6px 24px rgba(0,0,0,.45))}
        .analog .d{font:600 1.2em 'Segoe UI Variable Text','Segoe UI',sans-serif;letter-spacing:.2em;text-transform:uppercase;text-shadow:0 1px 8px rgba(0,0,0,.6)}
        </style>
        <div id=clock class="__STYLE__ __POS__"><div class=t></div><div class=d></div></div>
        <script>(()=>{
          const el=document.getElementById('clock');
          if(!location.search.includes('main')){el.remove();return;} // one clock, on the main screen only
          const H24=__H24__,DATE=__DATE__,style='__STYLE__',t=el.querySelector('.t'),d=el.querySelector('.d'),pad=n=>String(n).padStart(2,'0');
          if(style==='analog')t.innerHTML=`<svg viewBox="0 0 200 200"><defs><radialGradient id=g><stop offset=0 stop-color="rgba(20,30,50,.55)"/><stop offset=1 stop-color="rgba(5,8,16,.8)"/></radialGradient></defs>
            <circle cx=100 cy=100 r=96 fill="url(#g)" stroke="rgba(34,228,255,.75)" stroke-width=2 />
            ${Array.from({length:60},(_,i)=>`<line x1=100 y1=${i%5?10:8} x2=100 y2=${i%5?14:22} stroke="${i%5?'rgba(255,255,255,.35)':'#fff'}" stroke-width=${i%5?1:3} stroke-linecap=round transform="rotate(${i*6} 100 100)"/>`).join('')}
            <line id=hh x1=100 y1=100 x2=100 y2=52 stroke="#fff" stroke-width=6 stroke-linecap=round />
            <line id=mm x1=100 y1=100 x2=100 y2=30 stroke="#fff" stroke-width=4 stroke-linecap=round />
            <line id=ss x1=100 y1=112 x2=100 y2=24 stroke="#22e4ff" stroke-width=2 stroke-linecap=round />
            <circle cx=100 cy=100 r=5 fill="#22e4ff"/></svg>`;
          const turn=(id,deg)=>t.querySelector('#'+id).setAttribute('transform',`rotate(${deg} 100 100)`);
          (function tick(){
            const n=new Date();
            if(style==='analog'){const s=n.getSeconds(),m=n.getMinutes()+s/60;turn('hh',(n.getHours()%12+m/60)*30);turn('mm',m*6);turn('ss',s*6);}
            else{
              let h=n.getHours();const ap=h<12?'AM':'PM';if(!H24)h=h%12||12;
              const txt=`${H24?pad(h):h}:${pad(n.getMinutes())}`;
              t.dataset.ghost=txt.replace(/\d/g,'8');
              t.innerHTML=txt+(style==='neon'?`<span class=x>${pad(n.getSeconds())}</span>`:'')+(H24?'':`<span class=x>${ap}</span>`);
            }
            d.textContent=DATE?n.toLocaleDateString('__LOCALE__',{weekday:'long',day:'numeric',month:'long'}):'';
            setTimeout(tick,1000-n.getMilliseconds());
          })();
        })();</script>
        """;

    const string Particles = """
        const C={snow:{n:140,col:'255,255,255',r:[1,3.2],vy:[.3,1.1],vx:[-.3,.3],a:.85},
          rain:{n:220,col:'180,200,255',len:[10,22],vy:[9,15],vx:[-1.5,-1],a:.45},
          sakura:{n:60,col:'255,183,197',r:[3,6],vy:[.5,1.3],vx:[.3,1],a:.9,spin:1},
          embers:{n:90,col:'255,140,40',r:[.8,2.4],vy:[-1.4,-.4],vx:[-.3,.3],a:.9,glow:1},
          stars:{n:180,col:'255,255,255',r:[.4,1.6],vy:[0,0],vx:[0,0],a:1,twinkle:1}}['__KIND__'];
        const S=__SPEED__,d=devicePixelRatio,cv=document.getElementById('fx'),g=cv.getContext('2d');let W,H;
        const rnd=([a,b])=>a+Math.random()*(b-a);
        const size=()=>{W=cv.width=innerWidth*d;H=cv.height=innerHeight*d;};
        const make=(p,edge)=>{p.x=Math.random()*W;p.y=edge?(C.vy[0]<0?H+10:-10):Math.random()*H;p.r=rnd(C.r||[1,1])*d;
          p.l=rnd(C.len||[0,0])*d;p.vy=rnd(C.vy)*d;p.vx=rnd(C.vx)*d;p.t=Math.random()*6.28;return p;};
        size();addEventListener('resize',size);
        const P=Array.from({length:C.n},()=>make({},false));
        (function tick(){if(window.paused){requestAnimationFrame(tick);return;}g.clearRect(0,0,W,H);
          for(const p of P){p.x+=p.vx/S+(C.spin?Math.sin(p.t)*.6:0);p.y+=p.vy/S;p.t+=.02/S;
            if(p.y>H+20||p.y<-20||p.x<-20||p.x>W+20)make(p,true);
            const a=C.twinkle?C.a*(.5+.5*Math.sin(p.t*3)):C.a;g.fillStyle=g.strokeStyle=`rgba(${C.col},${a})`;
            if(C.len){g.lineWidth=d;g.beginPath();g.moveTo(p.x,p.y);g.lineTo(p.x+p.vx*2,p.y+p.l);g.stroke();}
            else if(C.spin){g.save();g.translate(p.x,p.y);g.rotate(p.t);g.beginPath();g.ellipse(0,0,p.r,p.r*.6,0,0,6.28);g.fill();g.restore();}
            else{if(C.glow){g.shadowBlur=8*d;g.shadowColor=`rgb(${C.col})`;}g.beginPath();g.arc(p.x,p.y,p.r,0,6.28);g.fill();g.shadowBlur=0;}}
          requestAnimationFrame(tick);})();
        """;

    /// Glitch overlay: idle most of the time, then a short burst of displaced image slices, RGB-split bars and jitter.
    const string Glitch = """
        const S=__SPEED__,d=devicePixelRatio,m=document.getElementById('m'),cv=document.getElementById('fx'),g=cv.getContext('2d');
        let W,H,burst=0;
        const size=()=>{W=cv.width=innerWidth*d;H=cv.height=innerHeight*d;};size();addEventListener('resize',size);
        const r=(a,b)=>a+Math.random()*(b-a);
        const cover=()=>{const iw=m&&(m.videoWidth||m.naturalWidth),ih=m&&(m.videoHeight||m.naturalHeight);
          if(!iw||!ih)return null;const s=Math.max(W/iw,H/ih);return{s,ox:(W-iw*s)/2,oy:(H-ih*s)/2};}; // same crop as object-fit:cover
        (function tick(){
          if(window.paused){requestAnimationFrame(tick);return;}
          g.clearRect(0,0,W,H);
          if(burst>0&&!window.calm){
            burst--;const c=cover();
            for(let i=0,n=r(3,9)|0;i<n;i++){
              const y=r(0,H),h=r(4,70)*d,dx=r(-90,90)*d;
              if(c)try{g.drawImage(m,-c.ox/c.s,(y-c.oy)/c.s,W/c.s,h/c.s,dx,y,W,h);}catch(e){} // slice of the wallpaper, shifted sideways
              g.globalCompositeOperation='screen';
              g.fillStyle=Math.random()<.5?'rgba(255,0,70,.28)':'rgba(0,240,255,.28)';g.fillRect(dx*.5,y,W,h*r(.2,1));
              g.globalCompositeOperation='source-over';
            }
            if(Math.random()<.25){g.fillStyle='rgba(255,255,255,.07)';g.fillRect(0,0,W,H);}
            document.body.style.transform=`translate(${r(-6,6)}px,${r(-2,2)}px)`;
            document.body.style.filter=Math.random()<.5?`hue-rotate(${r(-40,40)}deg) saturate(1.4)`:'';
          }else{
            document.body.style.transform=document.body.style.filter='';
            if(!window.calm&&Math.random()<1/(170*S))burst=r(6,20)|0; // a burst every ~3 s at normal speed; none during a snapshot
          }
          requestAnimationFrame(tick);
        })();
        """;

    readonly List<WebView2> views = new();
    WebView2 primaryView;
    string current;

    /// Snapshot of the main screen's wallpaper as it looks now, minus the desktop clock (the lock screen has its own)
    /// and minus any glitch burst. Null when no wallpaper is playing.
    public async Task<string> CaptureForLockScreen()
    {
        var cw = primaryView?.CoreWebView2;
        if (cw == null) return null;
        await cw.ExecuteScriptAsync("window.calm=true;document.getElementById('clock')?.style.setProperty('visibility','hidden')");
        await Task.Delay(150); // let a frame render without them
        var file = Path.Combine(Settings.Dir, "lockscreen.png");
        try
        {
            await using var fs = File.Create(file);
            await cw.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
        }
        finally { await cw.ExecuteScriptAsync("window.calm=false;document.getElementById('clock')?.style.removeProperty('visibility')"); }
        return file;
    }

    bool paused;

    /// Freezes or resumes the wallpaper (video, CSS animations, canvas overlays) without tearing it down.
    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        foreach (var v in views) _ = v.CoreWebView2?.ExecuteScriptAsync($"window.setPaused&&setPaused({(value ? "true" : "false")})");
    }

    public void Show(string media, bool clockEverywhere = false)
    {
        var key = media + "|" + clockEverywhere;
        if (forms.Count > 0 && current == key) { foreach (var v in views) v.CoreWebView2?.Reload(); return; } // only the look changed
        Stop();
        current = key;
        this.clockEverywhere = clockEverywhere;
        var (parent, insertAfter, layered) = DesktopLayer();
        var vs = SystemInformation.VirtualScreen;
        foreach (var screen in Display.All())
        {
            var f = new WallForm { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, BackColor = Color.Black };
            var wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            f.Controls.Add(wv);
            f.Show();
            if (layered) // Win11 24H2+: children of Progman must be layered to be drawn
            {
                Native.SetWindowLong(f.Handle, -20, Native.GetWindowLong(f.Handle, -20) | 0x80000);
                Native.SetLayeredWindowAttributes(f.Handle, 0, 255, 2);
            }
            // a popup can't be re-parented: turn it into a real child window first (WS_POPUP off, WS_CHILD on)
            Native.SetWindowLong(f.Handle, -16, (int)((uint)Native.GetWindowLong(f.Handle, -16) & ~0x80000000u) | 0x40000000);
            Native.SetParent(f.Handle, parent);
            var b = screen.Bounds;
            Native.SetWindowPos(f.Handle, insertAfter, b.X - vs.X, b.Y - vs.Y, b.Width, b.Height, 0x0010 | 0x0040); // NOACTIVATE | SHOWWINDOW
            Log($"wallpaper window 0x{f.Handle:X} parent=0x{Native.GetParent(f.Handle):X} wanted=0x{parent:X} layered={layered} bounds={b}");
            forms.Add(f); views.Add(wv);
            if (screen.Primary) primaryView = wv;
            Load(wv, media, screen.Primary);
        }
    }

    bool clockEverywhere;

    async void Load(WebView2 wv, string media, bool primary)
    {
        try
        {
            await wv.EnsureCoreWebView2Async(await Web.Env());
            wv.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local", Settings.Dir, CoreWebView2HostResourceAccessKind.Allow);
            if (!string.IsNullOrEmpty(media)) // "" = solid color desktop, nothing to serve
                wv.CoreWebView2.SetVirtualHostNameToFolderMapping("media.local", Path.GetDirectoryName(media), CoreWebView2HostResourceAccessKind.Allow);
            wv.CoreWebView2.NavigationCompleted += (_, _) => { if (paused) _ = wv.CoreWebView2.ExecuteScriptAsync("window.setPaused&&setPaused(true)"); }; // reloads keep the pause
            Log($"wallpaper page loading, clock={(primary || clockEverywhere ? "yes" : "no")}");
            // "main" = this screen shows the clock (main screen only, unless the clock is set to every screen)
            wv.CoreWebView2.Navigate("https://app.local/wallpaper.html" + (primary || clockEverywhere ? "?main" : ""));
        }
        catch (ObjectDisposedException) { } // stopped before it finished loading
        catch (Exception e) { Log("wallpaper load failed: " + e); MessageBox.Show("Wallpaper failed: " + e.Message, "Astra Desk"); }
    }

    public static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Settings.Dir, "log.txt");
            if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Move(path, Path.Combine(Settings.Dir, "log.old.txt"), true); // keep the log small
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    public void Stop()
    {
        Log($"wallpaper stop ({forms.Count} windows)");
        if (forms.Count == 0) return;
        foreach (var f in forms) f.Close();
        forms.Clear(); views.Clear(); primaryView = null; current = null; paused = false;
        // repaint the normal wallpaper over the last video frame
        var still = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "") as string;
        Native.SystemParametersInfo(0x14, 0, still ?? "", 0);
    }

    /// Where to put a window so it sits above the wallpaper but below desktop icons.
    static (IntPtr parent, IntPtr insertAfter, bool layered) DesktopLayer()
    {
        var progman = Native.FindWindow("Progman", null);
        Native.SendMessageTimeout(progman, 0x052C, 0xD, 1, 0, 1000, out _); // ask Explorer to spawn the WorkerW layer
        Native.SendMessageTimeout(progman, 0x052C, 0, 0, 0, 1000, out _);
        var defView = Tweaks.FindDefView();
        var host = Native.GetParent(defView);
        if (host == progman) return (progman, defView, true); // 24H2+: icons stay inside Progman
        var worker = Native.FindWindowEx(0, host, "WorkerW", null); // classic: the WorkerW right after the icons' host
        return (worker != 0 ? worker : progman, 0, false);
    }
}

static class Tiler
{
    static Rectangle Slice(Rectangle a, int i, int count, bool vertical)
    {
        if (vertical) { int y0 = a.Y + a.Height * i / count, y1 = a.Y + a.Height * (i + 1) / count; return new Rectangle(a.X, y0, a.Width, y1 - y0); }
        int x0 = a.X + a.Width * i / count, x1 = a.X + a.Width * (i + 1) / count; return new Rectangle(x0, a.Y, x1 - x0, a.Height);
    }

    public static Rectangle[] Tile(Rectangle a, int n, string mode)
    {
        if (n <= 0) return [];
        if (mode == "Columns") return Enumerable.Range(0, n).Select(i => Slice(a, i, n, false)).ToArray();
        if (mode == "Main" && n > 1)
        {
            var main = new Rectangle(a.X, a.Y, a.Width * 3 / 5, a.Height);
            var side = new Rectangle(main.Right, a.Y, a.Right - main.Right, a.Height);
            return [main, .. Enumerable.Range(0, n - 1).Select(i => Slice(side, i, n - 1, true))];
        }
        int cols = (int)Math.Ceiling(Math.Sqrt(n)), rows = (n + cols - 1) / cols;
        return Enumerable.Range(0, n).Select(i =>
        {
            int row = i / cols, inRow = row == rows - 1 ? n - cols * row : cols; // last row stretches
            return Slice(Slice(a, row, rows, true), i % cols, inRow, false);
        }).ToArray();
    }

    public static void SelfTest()
    {
        var area = new Rectangle(-1920, 40, 1920, 1040);
        foreach (var mode in new[] { "Columns", "Grid", "Main" })
            for (int n = 1; n <= 10; n++)
            {
                var r = Tile(area, n, mode);
                if (r.Length != n) throw new Exception($"{mode}/{n}: count");
                if (r.Any(x => !area.Contains(x) || x.Width <= 0 || x.Height <= 0)) throw new Exception($"{mode}/{n}: out of area");
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) if (r[i].IntersectsWith(r[j])) throw new Exception($"{mode}/{n}: overlap");
                if (r.Sum(x => (long)x.Width * x.Height) != (long)area.Width * area.Height) throw new Exception($"{mode}/{n}: gaps");
            }
    }

    public static void Apply(string mode)
    {
        foreach (var group in AppWindows(includeMinimized: false).GroupBy(Screen.FromHandle))
        {
            var list = group.ToList();
            var rects = Tile(group.Key.WorkingArea, list.Count, mode);
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i]; var r = rects[i];
                if (Native.IsZoomed(h)) Native.ShowWindow(h, 9); // restore
                // Win10/11 windows have invisible resize borders; grow by them so tiles meet edge to edge
                Native.GetWindowRect(h, out var wr);
                Native.DwmGetWindowAttribute(h, 9, out Native.RECT fr, Marshal.SizeOf<Native.RECT>());
                Native.SetWindowPos(h, 0, r.X - (fr.L - wr.L), r.Y - (fr.T - wr.T),
                    r.Width + (fr.L - wr.L) + (wr.R - fr.R), r.Height + (fr.T - wr.T) + (wr.B - fr.B), 0x0004 | 0x0010); // NOZORDER | NOACTIVATE
            }
        }
    }

    /// Top-level windows a user would call "open apps" (what the taskbar shows).
    public static List<IntPtr> AppWindows(bool includeMinimized)
    {
        var list = new List<IntPtr>();
        Native.EnumWindows((h, _) => { if (IsAppWindow(h, includeMinimized)) list.Add(h); return true; }, 0);
        return list;
    }

    static bool IsAppWindow(IntPtr h, bool includeMinimized)
    {
        if (!Native.IsWindowVisible(h) || (!includeMinimized && Native.IsIconic(h)) || Native.GetWindowTextLength(h) == 0 || Native.GetWindow(h, 4) != 0) return false;
        if ((Native.GetWindowLong(h, -20) & 0x80) != 0) return false; // tool windows
        Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4);
        if (cloaked != 0) return false; // hidden UWP / other virtual desktops
        Native.GetWindowThreadProcessId(h, out uint pid);
        if (pid == Environment.ProcessId) return false;
        var cls = new StringBuilder(64); Native.GetClassName(h, cls, 64);
        return cls.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }
}

static class Tweaks
{
    const string RunKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
    const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    const string SearchKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search";
    const string ThemeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string TrayKey = @"Control Panel\NotifyIconSettings";

    /// Windows' own taskbar options (the same values the Settings app writes).
    static readonly Dictionary<string, (string Key, string Name, int Default)> TaskbarItems = new()
    {
        ["search"] = (SearchKey, "SearchboxTaskbarMode", 1), // 0 hidden, 1 icon, 2 box
        ["taskView"] = (AdvancedKey, "ShowTaskViewButton", 1),
        ["widgets"] = (AdvancedKey, "TaskbarDa", 1),
        ["seconds"] = (AdvancedKey, "ShowSecondsInSystemClock", 0),
        ["badges"] = (AdvancedKey, "TaskbarBadges", 1),
        ["combine"] = (AdvancedKey, "TaskbarGlomLevel", 0), // 0 always, 1 when full, 2 never
        ["align"] = (AdvancedKey, "TaskbarAl", 1), // 0 left, 1 center
    };

    public static Dictionary<string, int> ReadTaskbarItems() =>
        TaskbarItems.ToDictionary(p => p.Key, p => Registry.GetValue(p.Value.Key, p.Value.Name, p.Value.Default) as int? ?? p.Value.Default);

    public static void SetTaskbarItem(string id, int value)
    {
        if (!TaskbarItems.TryGetValue(id, out var item)) throw new ArgumentException("Unknown taskbar item " + id);
        try { Registry.SetValue(item.Key, item.Name, Math.Clamp(value, 0, 2), RegistryValueKind.DWord); }
        catch (UnauthorizedAccessException) { throw new Exception("Windows blocks changing this one. Use Settings > Personalization > Taskbar."); }
        Broadcast("TraySettings");
    }

    public static IEnumerable<object> TrayIcons()
    {
        using var root = Registry.CurrentUser.OpenSubKey(TrayKey);
        if (root == null) return [];
        var list = new List<(string id, string name, bool on)>();
        foreach (var id in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(id);
            if (key?.GetValue("ExecutablePath") is not string exe) continue;
            var name = key.GetValue("InitialTooltip") as string;
            list.Add((id, string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(exe) : name, key.GetValue("IsPromoted") is 1));
        }
        return list.OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase).Select(t => (object)new { t.id, t.name, t.on }).ToList();
    }

    public static void SetTrayIcon(string id, bool on)
    {
        if (string.IsNullOrEmpty(id) || id.Contains('\\')) throw new ArgumentException("Bad tray icon id");
        using var key = Registry.CurrentUser.OpenSubKey(TrayKey + "\\" + id, true) ?? throw new ArgumentException("Tray icon not found");
        key.SetValue("IsPromoted", on ? 1 : 0, RegistryValueKind.DWord);
    }

    public static IntPtr FindDefView()
    {
        IntPtr found = 0;
        Native.EnumWindows((top, _) => { found = Native.FindWindowEx(top, 0, "SHELLDLL_DefView", null); return found == 0; }, 0);
        return found;
    }

    static IntPtr DesktopIcons() => Native.FindWindowEx(FindDefView(), 0, "SysListView32", null);
    public static bool DesktopIconsVisible => Native.IsWindowVisible(DesktopIcons());
    /// Same command as right-click desktop > View > Show desktop icons, so Windows remembers it and its menu stays in sync.
    public static void SetDesktopIcons(bool on)
    {
        if (DesktopIconsVisible != on) Native.SendMessageTimeout(FindDefView(), 0x0111, 0x7402, 0, 2, 1000, out _); // WM_COMMAND
    }

    /// The normal Windows wallpaper file, or "" when the desktop is a solid color.
    public static string StillWallpaper() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "") is string p && p.Length > 0 && File.Exists(p) ? p : "";

    /// The solid desktop color ("R G B" in the registry) as CSS.
    public static string DesktopColor()
    {
        var parts = (Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Colors", "Background", "0 0 0") as string ?? "").Split(' ');
        return parts.Length == 3 && parts.All(x => byte.TryParse(x, out _)) ? $"rgb({parts[0]},{parts[1]},{parts[2]})" : "#000";
    }

    public static bool IsDark => Registry.GetValue(ThemeKey, "AppsUseLightTheme", 1) is 0;

    public static void DarkMode(bool dark)
    {
        Registry.SetValue(ThemeKey, "AppsUseLightTheme", dark ? 0 : 1);
        Registry.SetValue(ThemeKey, "SystemUsesLightTheme", dark ? 0 : 1);
        Broadcast("ImmersiveColorSet");
    }

    /// Start menu entry so the app can be found by typing its name; re-pointed if the exe moved.
    public static void EnsureStartMenuShortcut()
    {
        if (!Installer.IsInstalled && File.Exists(Installer.InstalledExe)) return; // an installed copy owns the Start menu entry
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Astra Desk.lnk"), Environment.ProcessPath);
    }

    /// Creates or re-points a .lnk shortcut.
    public static void CreateShortcut(string lnk, string target)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic sc = shell.CreateShortcut(lnk);
        if (File.Exists(lnk) && string.Equals((string)sc.TargetPath, target, StringComparison.OrdinalIgnoreCase)) return;
        sc.TargetPath = target;
        sc.WorkingDirectory = Path.GetDirectoryName(target);
        sc.Description = "Customize your taskbar, wallpaper and desktop";
        sc.Save();
    }

    public static bool StartsWithWindows => Registry.GetValue(RunKey, "DesktopStyler", null) != null;

    public static void SetStartWithWindows(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (on) k.SetValue("DesktopStyler", $"\"{Environment.ProcessPath}\" --tray"); else k.DeleteValue("DesktopStyler", false);
    }

    static void Broadcast(string area) => Native.SendMessageTimeout(0xFFFF, 0x001A, 0, area, 2, 1000, out _);
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    public delegate bool EnumProc(IntPtr h, IntPtr l);

    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    public static void DarkTitleBar(IntPtr h, bool dark) { int v = dark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref v, 4); }

    /// Paints a per-pixel-alpha bitmap onto a WS_EX_LAYERED window.
    public static void PushLayered(IntPtr h, Bitmap bmp, int x, int y)
    {
        IntPtr screen = GetDC(0), mem = CreateCompatibleDC(screen), hbmp = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hbmp);
        try
        {
            var pos = new Point(x, y); var size = bmp.Size; var src = Point.Empty;
            var blend = new BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 }; // AC_SRC_ALPHA
            UpdateLayeredWindow(h, screen, ref pos, ref size, mem, ref src, 0, ref blend, 2); // ULW_ALPHA
        }
        finally { SelectObject(mem, old); DeleteObject(hbmp); DeleteDC(mem); ReleaseDC(0, screen); }
    }

    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref Point pos, ref Size size, IntPtr src, ref Point srcPos, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int idx, int val);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] public static extern IntPtr GetClassLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, string pv, uint winIni);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT val, int size);
    [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);
    [DllImport("powrprof.dll")] public static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll")] public static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);
    [DllImport("powrprof.dll")] public static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);
    [DllImport("kernel32.dll")] public static extern IntPtr LocalFree(IntPtr p);
    [DllImport("user32.dll")] public static extern bool LockWorkStation();
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize, dwTime; }
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CreateHardLink(string link, string target, IntPtr reserved);
}

/// Windows' idle timeouts. A playing video wallpaper holds "display required", which would stop Windows from starting the
/// screensaver or turning the screen off; the app pauses the wallpaper just before the first of these timeouts.
static class IdleTimeouts
{
    static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99"), VideoIdle = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

    /// Screensaver timeout in seconds, 0 when no screensaver is set up.
    public static int ScreensaverSeconds()
    {
        int active = 0, seconds = 0;
        Native.SystemParametersInfo(16, 0, ref active, 0);  // SPI_GETSCREENSAVEACTIVE
        Native.SystemParametersInfo(14, 0, ref seconds, 0); // SPI_GETSCREENSAVETIMEOUT
        bool hasOne = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "SCRNSAVE.EXE", "") is string exe && exe.Length > 0;
        return active != 0 && hasOne ? seconds : 0;
    }

    /// "Turn off my screen after" from the active power plan (plugged in or on battery), 0 = never.
    public static int ScreenOffSeconds(bool onBattery)
    {
        try
        {
            if (Native.PowerGetActiveScheme(IntPtr.Zero, out var ptr) != 0) return 0;
            var scheme = Marshal.PtrToStructure<Guid>(ptr); Native.LocalFree(ptr);
            Guid sub = VideoSubgroup, setting = VideoIdle;
            uint value;
            uint err = onBattery ? Native.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out value)
                                 : Native.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out value);
            return err == 0 ? (int)value : 0;
        }
        catch { return 0; }
    }

    /// Idle seconds after which the wallpaper pauses: a few seconds before the earliest timeout (0 = never).
    public static int PauseAfter(int screensaverSeconds, int screenOffSeconds)
    {
        var limits = new[] { screensaverSeconds, screenOffSeconds }.Where(x => x > 0).ToList();
        return limits.Count == 0 ? 0 : Math.Max(10, limits.Min() - 5);
    }

    /// What to do after `idle` seconds without input. Windows restarts its own idle clock when the video's "display
    /// required" request ends, so it would act a whole timeout late; the app acts on the timeouts itself instead.
    /// Returns "screensaver", "screen-off" or null; `done` = already acted during this idle period.
    public static string Action(uint idle, int screensaverSeconds, int screenOffSeconds, bool saverRunning, bool done)
    {
        if (done || saverRunning) return null; // a running screensaver handles screen-off itself
        if (screensaverSeconds > 0 && idle >= screensaverSeconds) return "screensaver";
        if (screenOffSeconds > 0 && idle >= screenOffSeconds) return "screen-off";
        return null;
    }

    /// Turns the monitor(s) off now, exactly like Windows' screen-off timeout; any input turns them back on.
    public static void TurnScreenOff() => Native.SendMessageTimeout(0xFFFF, 0x0112, 0xF170, 2, 2, 1000, out _); // WM_SYSCOMMAND SC_MONITORPOWER off

    public static uint IdleSeconds() { var i = new Native.LASTINPUTINFO { cbSize = 8 }; Native.GetLastInputInfo(ref i); return unchecked((uint)Environment.TickCount - i.dwTime) / 1000; }
}

/// A monitor the app draws on (custom bar, wallpaper, clock, screensaver). `--test-screens N` splits the main monitor into
/// N side-by-side fake screens so the multi-monitor paths can be tested on a single-monitor PC.
record Display(Rectangle Bounds, string Name, bool Primary)
{
    public static int TestSplit;
    public static bool Testing => TestSplit >= 2;

    public static List<Display> All()
    {
        if (!Testing) return Screen.AllScreens.Select(s => new Display(s.Bounds, s.DeviceName, s.Primary)).ToList();
        var b = Screen.PrimaryScreen.Bounds;
        return Enumerable.Range(0, TestSplit).Select(i =>
        {
            int x0 = b.X + b.Width * i / TestSplit, x1 = b.X + b.Width * (i + 1) / TestSplit;
            return new Display(new Rectangle(x0, b.Y, x1 - x0, b.Height), "TEST" + (i + 1), i == 0);
        }).ToList();
    }

    /// The display holding most of a window (by its center), or the main one.
    public static Display Of(IntPtr window)
    {
        var all = All();
        Native.GetWindowRect(window, out var r);
        var center = new Point((r.L + r.R) / 2, (r.T + r.B) / 2);
        return all.FirstOrDefault(d => d.Bounds.Contains(center)) ?? all.First(d => d.Primary);
    }
}

/// Global keyboard shortcuts (Ctrl+Alt+key). One another app already owns is listed as unavailable instead of failing.
class Hotkeys : NativeWindow, IDisposable
{
    readonly List<Action> actions = new();
    public readonly List<(string Keys, string Action, bool Ok)> List = new();

    public Hotkeys(IEnumerable<(Keys Key, string Name, Action Run)> keys)
    {
        CreateHandle(new CreateParams { Parent = (IntPtr)(-3) }); // message-only window
        foreach (var (key, name, run) in keys)
        {
            bool ok = Native.RegisterHotKey(Handle, actions.Count, 0x0002 | 0x0001 | 0x4000, (uint)key); // Ctrl + Alt, no auto-repeat
            actions.Add(run);
            List.Add(($"Ctrl + Alt + {key}", name, ok));
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && (int)m.WParam is var id && id >= 0 && id < actions.Count) // WM_HOTKEY
            try { actions[id](); } catch (Exception e) { WallpaperHost.Log("shortcut failed: " + e.Message); }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        for (int i = 0; i < actions.Count; i++) Native.UnregisterHotKey(Handle, i);
        DestroyHandle();
    }
}

/// App icon, embedded in the exe (also used for windows and the tray).
static class AppIcon
{
    public static Icon Get(Size size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico");
        return stream == null ? SystemIcons.Application : new Icon(stream, size);
    }
}

/// `DesktopStyler.exe --selftest`: pure-logic checks, exits non-zero on the first failure.
static class SelfTest
{
    public static void Run()
    {
        Tiler.SelfTest();

        var s = new Settings();
        s.SetZones(["clock", "bogus", "clock", "start"], ["start", "apps"], ["apps", "volume", "start"]);
        Check(s.BarLeft.SequenceEqual(["clock", "start"]), "zones: drops unknown ids and duplicates");
        Check(s.BarCenter.SequenceEqual(["apps"]), "zones: center skips ids already on the left");
        Check(s.BarRight.SequenceEqual(["volume"]), "zones: right skips ids used elsewhere");
        var d = new Settings();
        Check(d.BarLeft.Concat(d.BarCenter).Concat(d.BarRight).All(id => Bar.Elements.Any(e => e.Id == id)), "defaults: every element id exists");

        Check(Settings.Parse("{\"TaskbarMode\":\"Hidden\"}").TaskbarMode == "Custom", "settings: old Hidden mode migrates to Custom");
        Check(Settings.Parse("not json").TaskbarMode == "Normal", "settings: corrupt file falls back to defaults");
        Check(Settings.Parse("{\"BarOpacity\":0}").BarOpacity == 0, "settings: 0% opacity survives a reload");

        Check(Bar.CenterStart(1000, 10, 990, 200) == 400, "bar: center zone is centered");
        Check(Bar.CenterStart(1000, 500, 990, 200) == 500, "bar: center never overlaps a wide left zone");
        Check(Bar.CenterStart(1000, 10, 450, 200) == 250, "bar: center never overlaps a wide right zone");
        Check(Bar.CenterStart(1000, 300, 600, 900) == 300, "bar: overflowing center hugs the left zone");

        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR"); // decimal comma must not leak into CSS
        static Settings Look(string motion, string overlay, string speed, string clock = "off") =>
            new() { WallpaperMotion = motion, WallpaperOverlay = overlay, WallpaperSpeed = speed, ClockStyle = clock };
        var html = WallpaperHost.Html(Look("sway", "snow", "slow"), @"C:\w\my clip.webm", animate: true);
        Thread.CurrentThread.CurrentCulture = culture;
        Check(html.Contains("animation:fx 21.6s") && !html.Contains("21,6"), "wallpaper: motion duration is valid CSS in any language");
        Check(html.Contains("my%20clip.webm") && html.Contains("<video"), "wallpaper: video file is referenced and escaped");
        Check(html.Contains("['snow']") && html.Contains("const S=1.8"), "wallpaper: overlay and speed are applied");
        var plain = WallpaperHost.Html(Look("none", "none", "normal"), @"C:\w\a.png", animate: true);
        Check(!plain.Contains("animation:fx") && !plain.Contains("<canvas") && plain.Contains("<img"), "wallpaper: no animation when none is chosen");
        var bogus = WallpaperHost.Html(Look("bogus", "bogus", "bogus", "bogus"), @"C:\w\a.png", animate: true);
        Check(!bogus.Contains("<canvas") && !bogus.Contains("animation:fx") && !bogus.Contains("id=clock"), "wallpaper: unknown options add no animation, overlay or clock");
        Check(!WallpaperHost.Html(Look("zoom", "snow", "normal"), @"C:\w\a.jpg", animate: false).Contains("<canvas"), "wallpaper: still Windows wallpaper behind the clock is not animated");
        var glitch = WallpaperHost.Html(Look("none", "glitch", "fast"), @"C:\w\a.mp4", animate: true);
        Check(glitch.Contains("<canvas id=fx>") && glitch.Contains("const S=0.5") && glitch.Contains("burst") && !glitch.Contains("__"), "wallpaper: glitch overlay with speed applied");

        var clock = WallpaperHost.Html(new Settings { ClockStyle = "neon", ClockPosition = "nowhere", ClockSize = "large", Clock24h = false }, "", animate: false);
        // size follows the screen width (16px on a 1920px screen), so the settings preview shows it to scale
        Check(clock.Contains("class=\"neon center\"") && clock.Contains("calc(.834vw*1.45)") && clock.Contains("const H24=false"), "clock: style, safe position, size and 12h mode");
        Check(!clock.Contains("<img") && !clock.Contains("<video") && (clock.Contains("background:rgb(") || clock.Contains("background:#000")), "clock: solid-color desktop has no media");
        Check(!WallpaperHost.Html(new Settings(), @"C:\w\a.png", animate: true).Contains("id=clock"), "clock: off by default");
        var off = new Settings { WallpaperEnabled = false, ClockStyle = "off", Wallpaper = @"C:\nope.mp4" };
        Check(WallpaperHost.MediaFor(off) == null, "clock: nothing drawn over Windows when wallpaper and clock are off");
        Check(WallpaperHost.MediaFor(new Settings { ClockStyle = "analog" }) != null, "clock: turning it on alone puts the desktop layer up");

        var px = Workshop.ParsePexels(JsonDocument.Parse("""
            {"videos":[{"id":1,"url":"https://www.pexels.com/video/neon-city-lights-123/","image":"https://i/1.jpg","duration":12,"user":{"name":"Ann"},
              "video_files":[{"file_type":"video/mp4","width":640,"link":"https://v/640"},{"file_type":"video/mp4","width":3840,"link":"https://v/3840"},
                             {"file_type":"video/mp4","width":2560,"link":"https://v/2560"},{"file_type":"video/mp4","width":null,"link":"https://v/x"}]},
             {"id":2,"url":"https://www.pexels.com/video/x-2/","video_files":[{"file_type":"video/webm","width":1920,"link":"w"}]}]}
            """).RootElement);
        Check(px.Count == 1 && px[0].Key == "pexels:1", "workshop: Pexels skips videos without an mp4");
        Check(px[0].Download == "https://v/2560" && px[0].Width == 2560, "workshop: picks the smallest file that covers 1080p");
        Check(px[0].Preview == "https://v/640" && px[0].Title == "Neon city lights" && px[0].Author == "Ann", "workshop: Pexels preview, title and credit");
        var pb = Workshop.ParsePixabay(JsonDocument.Parse("""
            {"hits":[{"id":7,"pageURL":"https://pixabay.com/videos/id-7/","tags":"anime, sky, clouds","duration":20,"user":"Bo",
              "videos":{"large":{"url":"","width":0},"medium":{"url":"https://p/1920","width":1920,"thumbnail":"https://p/m.jpg"},
                        "small":{"url":"https://p/1280","width":1280},"tiny":{"url":"https://p/960","width":960,"thumbnail":"https://p/t.jpg"}}}]}
            """).RootElement);
        Check(pb.Count == 1 && pb[0].Download == "https://p/1920" && pb[0].Preview == "https://p/960", "workshop: Pixabay ignores empty sizes");
        Check(pb[0].Thumb == "https://p/m.jpg" && pb[0].Title == "Anime, sky, clouds", "workshop: Pixabay thumbnail and title");
        Check(Workshop.Interleave([[px[0], px[0], px[0]], [pb[0]]]).Select(i => i.Source).SequenceEqual(["Pexels", "Pixabay", "Pexels", "Pexels"]), "workshop: sources are mixed fairly");
        Check(Throws(() => Workshop.Download("pexels:999", _ => { }).GetAwaiter().GetResult()), "workshop: only results it returned can be downloaded");
        var wh = Workshop.ParseWallhaven(JsonDocument.Parse("""
            {"data":[{"id":"n6l2qq","url":"https://wallhaven.cc/w/n6l2qq","path":"https://w.wallhaven.cc/full/n6/wallhaven-n6l2qq.jpg","dimension_x":2560,
                      "thumbs":{"large":"https://th.wallhaven.cc/lg/n6/n6l2qq.jpg"}},
                     {"id":"../evil","path":"https://x/y.jpg"}]}
            """).RootElement, "one piece");
        Check(wh.Count == 1 && wh[0].Kind == "image" && wh[0].Title == "One piece" && wh[0].Width == 2560, "workshop: Wallhaven anime results parse");
        Check(Workshop.FileExt(wh[0].Download) == ".jpg" && Workshop.FileExt("https://v/clip.mp4?x=1") == ".mp4" && Workshop.FileExt("https://v/a.exe") == ".mp4", "workshop: saved files only get media extensions");
        Check(SiteBrowser.IsMediaFile("Luffy Gear 5.MP4") && !SiteBrowser.IsMediaFile("setup.exe") && !SiteBrowser.IsMediaFile("pack.zip"), "browser: only videos and images are installed");
        Check(SiteBrowser.OnSite("www.moewalls.com", "moewalls.com") && !SiteBrowser.OnSite("evilmoewalls.com", "moewalls.com"), "browser: site check can't be fooled by look-alike domains");

        var ps = new Settings();
        Check(App.PauseReasonFor(ps, 5, false, false, false) == null, "pause: plays normally");
        Check(App.PauseReasonFor(ps, 3, false, false, false) != null && App.PauseReasonFor(ps, 2, false, false, false) != null, "pause: games and full-screen apps pause it");
        Check(App.PauseReasonFor(ps, 1, false, false, false) != null, "pause: locked screen / screensaver pauses it");
        Check(App.PauseReasonFor(ps, 5, true, false, false) != null && App.PauseReasonFor(new Settings { PauseOnBattery = false }, 5, true, false, false) == null, "pause: battery rule follows its switch");
        Check(App.PauseReasonFor(ps, 5, false, true, false) == null && App.PauseReasonFor(new Settings { PauseMaximized = true }, 5, false, true, false) != null, "pause: maximized rule is opt-in");
        Check(App.PauseReasonFor(ps, 5, false, false, true) == "paused by you", "pause: manual pause wins");
        Check(App.PauseReasonFor(ps, 5, false, false, false, away: true) != null, "pause: being away pauses it so the screen can turn off");
        Check(IdleTimeouts.PauseAfter(300, 600) == 295 && IdleTimeouts.PauseAfter(0, 600) == 595 && IdleTimeouts.PauseAfter(0, 0) == 0 && IdleTimeouts.PauseAfter(60, 0) == 55,
            "pause: away = 5 s before the earliest screensaver / screen-off timeout");
        Check(IdleTimeouts.PauseAfter(8, 0) == 10, "pause: never sooner than 10 s");
        Check(IdleTimeouts.Action(59, 60, 300, false, false) == null && IdleTimeouts.Action(60, 60, 300, false, false) == "screensaver", "idle: screensaver starts exactly at its timeout");
        Check(IdleTimeouts.Action(300, 0, 300, false, false) == "screen-off" && IdleTimeouts.Action(299, 0, 300, false, false) == null, "idle: screen turns off at the power-plan timeout when there's no screensaver");
        Check(IdleTimeouts.Action(900, 60, 300, true, false) == null && IdleTimeouts.Action(900, 60, 300, false, true) == null, "idle: acts once, and not while the screensaver runs");
        Check(IdleTimeouts.Action(10_000, 0, 0, false, false) == null, "idle: nothing happens when both timeouts are off");

        int acc = 0;
        Check(Bar.WheelSteps(ref acc, -120).SequenceEqual([false]) && acc == 0, "volume wheel: one notch down = one step down");
        Check(Bar.WheelSteps(ref acc, 40).Count == 0 && Bar.WheelSteps(ref acc, 40).Count == 0 && Bar.WheelSteps(ref acc, 40).SequenceEqual([true]), "volume wheel: small smooth-scroll deltas add up to one step");
        acc = 0;
        Check(Bar.WheelSteps(ref acc, 8).Count == 0 && acc == 8, "volume wheel: a tiny nudge changes nothing");
        Check(Bar.WheelSteps(ref acc, -120).SequenceEqual([false]) && acc == 0, "volume wheel: changing direction doesn't fire leftovers the wrong way");
        Check(Bar.WheelSteps(ref acc, 360).SequenceEqual([true, true, true]), "volume wheel: a fast spin gives one step per notch");
        Check(IdleTimeouts.ScreenOffSeconds(false) >= 0 && IdleTimeouts.ScreensaverSeconds() >= 0, "pause: Windows' timeouts can be read");
        Check(WallpaperHost.Html(new Settings(), @"C:\w\a.mp4", animate: true).Contains("window.setPaused"), "pause: wallpaper page can be paused");
        Check(WallpaperHost.Html(Look("none", "rain", "normal"), @"C:\w\a.mp4", animate: true).Contains("if(window.paused)"), "pause: particle overlays idle while paused");

        string[] pl = [@"C:\w\a.mp4", @"C:\w\b.jpg", @"C:\w\c.webm"];
        Check(App.NextInPlaylist(pl, @"C:\w\b.jpg", false, new Random(1)) == @"C:\w\c.webm" && App.NextInPlaylist(pl, @"C:\w\c.webm", false, new Random(1)) == @"C:\w\a.mp4", "playlist: in order, wrapping around");
        Check(App.NextInPlaylist(pl, @"C:\other.mp4", false, new Random(1)) == @"C:\w\a.mp4", "playlist: starts at the first file");
        Check(Enumerable.Range(0, 50).All(i => App.NextInPlaylist(pl, @"C:\w\A.MP4", true, new Random(i)) != @"C:\w\a.mp4"), "playlist: shuffle never repeats the current one");
        Check(App.NextInPlaylist([], "x", true, new Random()) == null && App.NextInPlaylist([@"C:\w\a.mp4"], @"C:\w\a.mp4", true, new Random()) == @"C:\w\a.mp4", "playlist: empty and single-file folders");

        Check(Screensaver.IsScreensaverArgs(["/s"]) && Screensaver.IsScreensaverArgs(["/c:1234"]) && Screensaver.IsScreensaverArgs(["/P"]), "screensaver: Windows' arguments are recognized");
        Check(!Screensaver.IsScreensaverArgs(["--selftest"]) && !Screensaver.IsScreensaverArgs(["--quit"]) && !Screensaver.IsScreensaverArgs([]), "screensaver: app arguments are not mistaken for it");
        Check(WallpaperHost.Locale().Length >= 2 && WallpaperHost.Html(new Settings { ClockStyle = "neon" }, "", false).Contains($"toLocaleDateString('{WallpaperHost.Locale()}'"), "clock: date follows Windows' regional format");
        Check(Regex.IsMatch(Installer.Version, @"^\d+\.\d+\.\d+$"), "installer: clean version number");

        Display.TestSplit = 2;
        try
        {
            var all = Display.All();
            Check(all.Count == 2 && all[0].Primary && !all[1].Primary && all[0].Bounds.Right == all[1].Bounds.Left
                  && all.Sum(d => d.Bounds.Width) == Screen.PrimaryScreen.Bounds.Width, "multi-monitor: test split makes 2 side-by-side screens");
            using var f = new Form { StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(all[1].Bounds.X + 40, all[1].Bounds.Y + 40, 300, 200), ShowInTaskbar = false };
            f.Show();
            Check(Display.Of(f.Handle).Name == "TEST2", "multi-monitor: a window on the 2nd screen belongs to that screen's bar");
            f.Location = new Point(all[0].Bounds.X + 40, all[0].Bounds.Y + 40);
            Check(Display.Of(f.Handle).Name == "TEST1", "multi-monitor: moving it to the 1st screen moves it to that bar");
        }
        finally { Display.TestSplit = 0; }
        Check(Display.All().Count == Screen.AllScreens.Length, "multi-monitor: real monitors when not testing");

        Check(Throws(() => Tweaks.SetTrayIcon(@"..\Run", true)), "tray: rejects ids that escape the registry key");
        Check(Throws(() => Tweaks.SetTaskbarItem("bogus", 1)), "taskbar items: only known switches are written");
    }

    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); }
    static bool Throws(Action a) { try { a(); return false; } catch { return true; } }
}
