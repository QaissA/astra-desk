import { Img, RevealObserver } from "./client";

// Change this if the .exe is hosted elsewhere (e.g. a GitHub release URL).
const DOWNLOAD_URL = "/downloads/DesktopStyler.exe";
const VERSION = "1.0.0";
const SIZE = "58 MB";

const features = [
  { cls: "c-wide", img: "feature-wallpaper.webp", tag: "Wallpaper", title: "Animated wallpapers with effects",
    alt: "Animated anime wallpaper with rain and glow overlay effects",
    text: "Videos, GIFs or images, with Motion and Overlay effects like sakura, rain and glitch. Build a playlist and the wallpaper pauses by itself during games or on battery." },
  { cls: "c-tall", img: "feature-taskbar.webp", tag: "Custom bar", title: "A taskbar you design",
    alt: "A see-through custom taskbar with pinned apps in left, center and right zones",
    text: "Fully transparent. Drag elements into left, center and right zones, pin your apps, one bar per screen." },
  { cls: "c-third", img: "feature-workshop.webp", tag: "Workshop", title: "Thousands of wallpapers",
    alt: "A grid of anime wallpaper thumbnails in the Workshop search",
    text: "Search Wallhaven anime art with no key, install MoeWalls and MotionBgs clips in one click, plus Pexels and Pixabay." },
  { cls: "c-third", img: "feature-clock.webp", tag: "Clock", title: "Desktop clock",
    alt: "A large neon desktop clock over a wallpaper",
    text: "Five styles, any corner, any size, 12 or 24 hours, on every screen." },
  { cls: "c-third", img: "feature-layout.webp", tag: "Window layout", title: "One-click tiling",
    alt: "Several app windows tiled neatly in a grid",
    text: "Columns, grid or focus: every open window snaps into place." },
  { cls: "c-half", img: "feature-screensaver.webp", tag: "Screensaver & lock screen", title: "Animated screensaver",
    alt: "An animated screensaver covering a dark monitor",
    text: "Your wallpaper becomes the screensaver after the idle time you choose, and the lock screen gets a matching snapshot." },
  { cls: "c-half", img: "app-settings.webp", tag: "The app", title: "Settings that look the part",
    alt: "The Desktop Styler settings window with a dark neon interface",
    text: "Taskbar, Custom bar, Workshop, Wallpaper, Window layout, Desktop. Open it from the Start menu, the ⚙ button or Ctrl + Alt + S." },
];

const effects = ["🌸 Sakura", "🌧 Rain", "⚡ Glitch", "✨ Motion", "🎞 Video", "🖼 GIF", "🔀 Shuffle playlist", "🎮 Pause in games", "🔋 Pause on battery", "🖥 Multi-monitor"];

const shortcuts: [string, string][] = [
  ["Next wallpaper", "W"], ["Pause / resume", "P"], ["Desktop icons", "D"],
  ["Custom bar", "B"], ["Tile windows", "T"], ["Open settings", "S"],
];

// Placeholders until the donation system is decided: set href and drop `disabled`.
const tiers = [
  { tag: "Coffee", price: "$3", text: "Buy a coffee for a late-night coding session.", hot: false },
  { tag: "Supporter", price: "$10", text: "Fund a new wallpaper effect or clock style.", hot: true },
  { tag: "Your choice", price: "Any", text: "Pick any amount, one time or monthly.", hot: false },
];

const faq: [string, React.ReactNode][] = [
  ["Is it really free?", "Yes. No ads, no account, no paid version. Donations are optional."],
  ["Will it slow down my games?", "No. The wallpaper pauses by itself when a game or full-screen video is running, when a window is maximized, when the PC is locked, and on battery if you turn that on."],
  ['Windows shows a "protected your PC" warning. Is that normal?', <>Windows SmartScreen warns about new apps it hasn&apos;t seen often yet. Click <b>More info</b>, then <b>Run anyway</b>.</>],
  ["Can I get my normal taskbar and wallpaper back?", "Yes. Quit from the settings sidebar and the Windows taskbar and wallpaper come back. Uninstalling from Settings > Apps also restores the taskbar, desktop icons, startup and screensaver settings."],
  ["Why is the lock screen a still picture?", "The Windows lock screen only supports still pictures, so it gets a snapshot of your wallpaper. The animated version is the screensaver."],
  ["Can I share the wallpapers I download?", "Anime art belongs to its creators and studios. Keep downloads on your own desktop and don't redistribute them. They're saved in Videos\\Wallpapers and stay there after uninstall."],
];

const WinIcon = () => (
  <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 5.5 10.5 4.5v7H3zm0 13 7.5 1v-7H3zM11.5 4.4 21 3v8.5h-9.5zm0 15.2L21 21v-8.5h-9.5z" /></svg>
);

export default function Home() {
  return (
    <>
      <RevealObserver />
      <div className="aurora" aria-hidden="true" />

      <nav>
        <div className="wrap">
          <a className="logo" href="#top"><Img src="/images/logo.png" alt="" />Desktop Styler</a>
          <div className="nav-links">
            <a href="#features">Features</a><a href="#shortcuts">Shortcuts</a><a href="#install">Install</a><a href="#support">Support</a><a href="#faq">FAQ</a>
          </div>
          <a className="btn btn-primary" href={DOWNLOAD_URL} download>Download</a>
        </div>
      </nav>

      <main id="top">
        <header className="hero">
          <div className="grid-bg" aria-hidden="true" />
          <div className="wrap">
            <span className="eyebrow reveal">Free for Windows 11</span>
            <h1 className="reveal">Your desktop,<br /><span className="grad-text">alive.</span></h1>
            <p className="lead reveal">Animated anime wallpapers with effects, a fully transparent custom taskbar, a desktop clock and one-click window layouts. One app, one click to install.</p>
            <div className="cta-row reveal">
              <a className="btn btn-primary" href={DOWNLOAD_URL} download><WinIcon />Download for Windows</a>
              <a className="btn btn-ghost" href="#features">See what it does</a>
            </div>
            <p className="cta-meta reveal"><b>v{VERSION}</b> · {SIZE} · single .exe · no admin needed</p>

            <div className="shot reveal">
              <div className="frame">
                <Img src="/images/hero-desktop.webp" fetchPriority="high"
                  alt="A Windows 11 desktop with an animated anime wallpaper, falling sakura petals and a transparent custom taskbar" />
              </div>
            </div>

            <div className="strip reveal">
              <span>Installs for your account only</span><span>Runs on WebView2, already in Windows 11</span>
              <span>Pauses during games and on battery</span><span>Uninstall restores everything</span>
            </div>
          </div>
        </header>

        <section id="features">
          <div className="wrap">
            <div className="center">
              <span className="eyebrow reveal">Features</span>
              <h2 className="reveal">Everything your desktop<br />was <span className="grad-text">missing</span>.</h2>
              <p className="lead reveal">Six pages of settings in a futuristic app, all built to stay out of your way when you work or play.</p>
            </div>

            <div className="bento">
              {features.map((f) => (
                <article key={f.img} className={`card ${f.cls} reveal`}>
                  <div className="media"><Img src={`/images/${f.img}`} alt={f.alt} loading="lazy" /></div>
                  <div className="body"><span className="tag">{f.tag}</span><h3>{f.title}</h3><p>{f.text}</p></div>
                </article>
              ))}
            </div>

            <div className="marquee reveal" aria-label="Wallpaper effects">
              <div className="track">
                {[...effects, ...effects].map((e, i) => (
                  <span key={i} className="chip" aria-hidden={i >= effects.length || undefined}>{e}</span>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section id="shortcuts">
          <div className="wrap center">
            <span className="eyebrow reveal">Keyboard first</span>
            <h2 className="reveal">Control it <span className="grad-text">without looking</span>.</h2>
            <div className="keys">
              {shortcuts.map(([label, k]) => (
                <div key={k} className="key reveal"><span>{label}</span><kbd>Ctrl + Alt + {k}</kbd></div>
              ))}
            </div>
          </div>
        </section>

        <section id="install">
          <div className="wrap install">
            <div>
              <span className="eyebrow reveal">Installer</span>
              <h2 className="reveal">Installed in <span className="grad-text">one click</span>.</h2>
              <p className="lead reveal">No setup wizard, no admin password, nothing else to install.</p>
              <ol className="steps">
                <li className="reveal"><div><b>Download DesktopStyler.exe</b><span>One file, {SIZE}.</span></div></li>
                <li className="reveal"><div><b>Double-click it and choose Yes</b><span>It installs for your account only, with a Start menu entry and a desktop shortcut.</span></div></li>
                <li className="reveal"><div><b>Pick a wallpaper and style your bar</b><span>Open it any time with Ctrl + Alt + S. Uninstall from Settings &gt; Apps whenever you like.</span></div></li>
              </ol>
            </div>
            <div className="dl-card reveal">
              <div className="media"><Img src="/images/installer.webp" alt="The Desktop Styler install prompt on a Windows 11 desktop" loading="lazy" /></div>
              <a className="btn btn-primary btn-block" href={DOWNLOAD_URL} download>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" aria-hidden="true"><path d="M12 3v12m0 0-5-5m5 5 5-5M4 20h16" /></svg>
                Download DesktopStyler.exe
              </a>
              <div className="specs">
                <div><b>Version</b>{VERSION}</div><div><b>Size</b>{SIZE}</div>
                <div><b>System</b>Windows 11</div><div><b>Rights</b>No admin needed</div>
              </div>
            </div>
          </div>
        </section>

        <section id="support">
          <div className="wrap">
            <div className="donate center">
              <div className="bgimg" aria-hidden="true"><Img src="/images/donate-bg.webp" alt="" loading="lazy" /></div>
              <span className="eyebrow reveal">Support the project</span>
              <h2 className="reveal">Free forever,<br /><span className="grad-text">powered by you</span>.</h2>
              <p className="lead reveal">Desktop Styler has no ads and no paid tier. If it made your desktop better, a donation keeps new features and wallpaper effects coming.</p>
              <div className="tiers">
                {tiers.map((t) => (
                  <div key={t.tag} className={`tier reveal${t.hot ? " hot" : ""}`}>
                    <span className="tag">{t.tag}</span>
                    <div className="price">{t.price}</div>
                    <p>{t.text}</p>
                    <button className={`btn ${t.hot ? "btn-primary" : "btn-ghost"}`} disabled>Coming soon</button>
                  </div>
                ))}
              </div>
              <p className="soon reveal">Donations open soon. Until then, sharing Desktop Styler with a friend helps just as much.</p>
            </div>
          </div>
        </section>

        <section id="faq">
          <div className="wrap">
            <div className="center">
              <span className="eyebrow reveal">FAQ</span>
              <h2 className="reveal">Good to know</h2>
            </div>
            <div className="faq">
              {faq.map(([q, a]) => (
                <details key={q} className="reveal"><summary>{q}</summary><p>{a}</p></details>
              ))}
            </div>
          </div>
        </section>

        <section className="final">
          <div className="grid-bg" aria-hidden="true" />
          <div className="wrap">
            <h2 className="reveal">Make Windows 11<br /><span className="grad-text">yours</span>.</h2>
            <div className="cta-row reveal">
              <a className="btn btn-primary" href={DOWNLOAD_URL} download>Download for Windows</a>
              <a className="btn btn-ghost" href="#support">Support the project</a>
            </div>
            <p className="cta-meta reveal"><b>v{VERSION}</b> · {SIZE} · free</p>
          </div>
        </section>
      </main>

      <footer>
        <div className="wrap">
          <span>© 2026 Desktop Styler</span>
          <span>Not affiliated with Microsoft. Windows is a trademark of Microsoft Corporation.</span>
        </div>
      </footer>
    </>
  );
}
