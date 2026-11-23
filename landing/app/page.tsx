import { ArrowDown, ArrowRight, Check, Download, Gamepad2, Zap } from "lucide-react";
import { guides } from "./guides";
import { HScroll, Img, Keycaps, Magnetic, Parallax, Reveal, RotatingWord, SpotCard, SplitWords, Tilt, Timeline, DonatePopup } from "./client";

// Change this if the .exe is hosted elsewhere (e.g. a GitHub release URL).
const DOWNLOAD_URL = "/downloads/AstraDesk-Setup.exe";
const VERSION = "1.0.1";
const SIZE = "51 MB";

const features = [
  { img: "feature-wallpaper.webp", tag: "Wallpaper", title: "Animated wallpapers with effects",
    alt: "Animated anime wallpaper with rain and glow overlay effects",
    text: "Videos, GIFs or images, with Motion and Overlay effects like sakura, rain and glitch. Build a playlist and the wallpaper pauses by itself during games or on battery." },
  { img: "feature-taskbar.webp", tag: "Custom bar", title: "A taskbar you design",
    alt: "A see-through custom taskbar with pinned apps in left, center and right zones",
    text: "Fully transparent. Drag elements into left, center and right zones, pin your apps, one bar per screen." },
  { img: "feature-workshop.webp", tag: "Workshop", title: "Thousands of wallpapers",
    alt: "A grid of anime wallpaper thumbnails in the Workshop search",
    text: "Search Wallhaven anime art with no key, install MoeWalls and MotionBgs clips in one click, plus Pexels and Pixabay." },
  { img: "feature-clock.webp", tag: "Clock", title: "Desktop clock",
    alt: "A large neon desktop clock over a wallpaper",
    text: "Five styles, any corner, any size, 12 or 24 hours, on every screen." },
  { img: "feature-layout.webp", tag: "Window layout", title: "One-click tiling",
    alt: "Several app windows tiled neatly in a grid",
    text: "Columns, grid or focus: every open window snaps into place." },
  { img: "feature-screensaver.webp", tag: "Screensaver & lock screen", title: "Animated screensaver",
    alt: "An animated screensaver covering a dark monitor",
    text: "Your wallpaper becomes the screensaver after the idle time you choose, and the lock screen gets a matching snapshot." },
  { img: "app-settings.webp", tag: "The app", title: "Settings that look the part",
    alt: "The Astra Desk settings window with a dark neon interface",
    text: "Taskbar, Custom bar, Workshop, Wallpaper, Window layout, Desktop. Open it from the Start menu, the ⚙ button or Ctrl + Alt + S." },
];

const effects = ["🌸 Sakura", "🌧 Rain", "⚡ Glitch", "✨ Motion", "🎞 Video", "🖼 GIF", "🔀 Shuffle playlist", "🎮 Pause in games", "🔋 Pause on battery", "🖥 Multi-monitor"];
const trust = ["Installs for your account only", "Runs on WebView2, already in Windows 11", "Pauses during games and on battery", "Uninstall restores everything"];

const shortcuts: [string, string][] = [
  ["Next wallpaper", "W"], ["Pause / resume", "P"], ["Desktop icons", "D"],
  ["Custom bar", "B"], ["Tile windows", "T"], ["Open settings", "S"],
];

const steps = [
  ["Download AstraDesk-Setup.exe", `One file, ${SIZE}.`],
  ["Run it and click Install", "It installs for your account only, with a Start menu entry you can search for and an optional desktop shortcut."],
  ["Pick a wallpaper and style your bar", "Open it any time with Ctrl + Alt + S. Uninstall from Settings > Apps whenever you like."],
];

const KOFI_URL = "https://ko-fi.com/astradesk";

const faq: [string, React.ReactNode][] = [
  ["Is it really free?", "Yes. No ads, no account, no paid version. Donations are optional."],
  ["Will it slow down my games?", "No. The wallpaper pauses by itself when a game or full-screen video is running, when a window is maximized, when the PC is locked, and on battery if you turn that on."],
  ['Windows shows a "protected your PC" warning. Is that normal?', <>Windows SmartScreen warns about new apps it hasn&apos;t seen often yet. Click <b>More info</b>, then <b>Run anyway</b>.</>],
  ["Can I get my normal taskbar and wallpaper back?", "Yes. Quit from the settings sidebar and the Windows taskbar and wallpaper come back. Uninstalling from Settings > Apps also restores the taskbar, desktop icons, startup and screensaver settings."],
  ["Why is the lock screen a still picture?", "The Windows lock screen only supports still pictures, so it gets a snapshot of your wallpaper. The animated version is the screensaver."],
  ["Can I share the wallpapers I download?", "Anime art belongs to its creators and studios. Keep downloads on your own desktop and don't redistribute them. They're saved in Videos\\Wallpapers and stay there after uninstall."],
];

// Deterministic so server and client render the same petals.
const petals = Array.from({ length: 18 }, (_, i) => ({
  left: `${(i * 37) % 100}%`, s: `${10 + ((i * 7) % 12)}px`, d: `${9 + ((i * 5) % 9)}s`,
  delay: `${-((i * 13) % 17)}s`, drift: `${((i * 29) % 160) - 80}px`,
}));

const WinIcon = () => (
  <svg viewBox="0 0 24 24" fill="currentColor" className="size-5" aria-hidden="true"><path d="M3 5.5 10.5 4.5v7H3zm0 13 7.5 1v-7H3zM11.5 4.4 21 3v8.5h-9.5zm0 15.2L21 21v-8.5h-9.5z" /></svg>
);

const Eyebrow = ({ children }: { children: React.ReactNode }) => (
  <span className="chip"><span className="size-1.5 rounded-full bg-mint shadow-[0_0_10px_var(--color-mint)]" />{children}</span>
);

function Tape({ items, className, reverse }: { items: string[]; className: string; reverse?: boolean }) {
  return (
    <div className={`overflow-hidden py-4 ${className}`}>
      <div className={`flex w-max gap-10 ${reverse ? "animate-marquee-rev" : "animate-marquee"}`}>
        {[...items, ...items].map((e, i) => (
          <span key={i} aria-hidden={i >= items.length || undefined} className="flex items-center gap-10 font-display text-lg font-bold whitespace-nowrap uppercase md:text-2xl">
            {e}<span className="opacity-50">✿</span>
          </span>
        ))}
      </div>
    </div>
  );
}

export default function Home() {
  return (
    <>
      {/* ambient blobs */}
      <div aria-hidden="true" className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
        <div className="animate-blob absolute -top-40 -left-40 size-[38rem] bg-sakura/25 blur-[120px]" />
        <div className="animate-blob absolute top-1/3 -right-52 size-[34rem] bg-plum blur-[110px] [animation-delay:-5s]" />
        <div className="animate-blob absolute -bottom-52 left-1/3 size-[30rem] bg-peach/15 blur-[120px] [animation-delay:-9s]" />
      </div>

      {/* floating pill nav */}
      <nav className="fixed inset-x-0 top-4 z-50 flex justify-center px-4">
        <div className="flex w-full max-w-4xl items-center justify-between gap-4 rounded-full border border-white/10 bg-ink/60 py-2 pr-2 pl-4 shadow-2xl shadow-black/40 backdrop-blur-xl">
          <a className="flex items-center gap-2.5 font-display text-sm font-bold" href="#top"><Img src="/images/logo-small.png" alt="" className="size-7 rounded-lg" />Astra Desk</a>
          <div className="hidden gap-7 text-sm text-mauve md:flex">
            {["Features", "Shortcuts", "Install", "FAQ"].map((l) => (
              <a key={l} href={`#${l.toLowerCase()}`} className="transition-colors hover:text-cream">{l}</a>
            ))}
          </div>
          <a className="btn btn-primary px-5! py-2.5! text-sm" href={DOWNLOAD_URL} download><Download className="size-4" />Download</a>
        </div>
      </nav>

      <main id="top">
        {/* HERO: split layout, kinetic headline, tilting screenshot with stickers, falling petals */}
        <header className="relative overflow-hidden pt-36 pb-24 lg:min-h-screen lg:pt-44">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0">
            {petals.map((p, i) => (
              <span key={i} className="petal" style={{ left: p.left, "--s": p.s, "--d": p.d, "--delay": p.delay, "--drift": p.drift } as React.CSSProperties} />
            ))}
          </div>

          <div className="mx-auto grid w-[min(1240px,100%-32px)] items-center gap-16 lg:grid-cols-[1.05fr_1fr]">
            <div>
              <Reveal><Eyebrow>Free for Windows 11</Eyebrow></Reveal>
              <h1 className="mt-7 text-[clamp(2.8rem,7vw,5.6rem)] font-black">
                {/* keyword line: the big words below say nothing to search engines */}
                <span className="mb-4 block font-sans text-base font-semibold tracking-normal text-mauve md:text-lg">Animated wallpapers &amp; a custom taskbar for Windows 11</span>
                <SplitWords text="Your desktop," delay={0.1} />
                <br />
                <SplitWords text="but" delay={0.3} /><RotatingWord words={["alive.", "dreamy.", "yours.", "cozy.", "louder."]} />
              </h1>
              <Reveal delay={0.5}>
                <p className="mt-7 max-w-xl text-lg text-mauve">Animated anime wallpapers with effects, a fully transparent custom taskbar, a desktop clock and one-click window layouts. One app, one click to install.</p>
              </Reveal>
              <Reveal delay={0.65} className="mt-9 flex flex-wrap items-center gap-4">
                <Magnetic><a className="btn btn-primary" href={DOWNLOAD_URL} download><WinIcon />Download for Windows</a></Magnetic>
                <a className="btn btn-ghost group" href="#features">See what it does<ArrowDown className="size-4 transition-transform group-hover:translate-y-1" /></a>
              </Reveal>
              <Reveal delay={0.8}><p className="mt-5 text-sm text-mauve"><b className="font-medium text-cream">v{VERSION}</b> · {SIZE} · one installer · no admin needed</p></Reveal>
            </div>

            <Reveal delay={0.3} y={60}>
              <Tilt className="relative">
                <div className="absolute -inset-6 -z-10 rounded-[2.5rem] bg-gradient-to-br from-sakura/50 via-peach/30 to-butter/30 blur-3xl" />
                <div className="overflow-hidden rounded-3xl border border-white/15 bg-ink-2 shadow-2xl shadow-black/60">
                  <div className="flex items-center gap-2 border-b border-white/10 px-4 py-3">
                    <span className="size-3 rounded-full bg-coral" /><span className="size-3 rounded-full bg-butter" /><span className="size-3 rounded-full bg-mint" />
                    <span className="ml-3 text-xs text-mauve">desktop.styler</span>
                  </div>
                  <div className="aspect-video bg-[radial-gradient(circle_at_30%_30%,var(--color-plum),var(--color-ink-2)_70%)]">
                    <Img src="/images/hero-desktop.webp" fetchPriority="high" className="size-full object-cover"
                      alt="A Windows 11 desktop with an animated anime wallpaper, falling sakura petals and a transparent custom taskbar" />
                  </div>
                </div>
                {/* stickers float in front thanks to translateZ inside the 3D tilt */}
                <div className="absolute -top-6 -left-6 [transform:translateZ(60px)]">
                  <div className="animate-float rounded-2xl border border-white/15 bg-ink/80 px-4 py-3 backdrop-blur-xl [--r:-6deg]">
                    <div className="font-display text-2xl font-bold">21:47</div><div className="text-xs text-mauve">neon clock</div>
                  </div>
                </div>
                <div className="absolute top-1/3 -right-5 [transform:translateZ(80px)]">
                  <div className="animate-float rounded-full bg-sakura px-4 py-2 text-sm font-semibold text-ink [--r:5deg] [animation-delay:-2s]">🌸 Sakura on</div>
                </div>
                <div className="absolute -bottom-6 left-10 [transform:translateZ(50px)]">
                  <div className="animate-float flex items-center gap-2 rounded-full border border-mint/40 bg-ink/80 px-4 py-2 text-sm backdrop-blur-xl [--r:-3deg] [animation-delay:-4s]">
                    <Gamepad2 className="size-4 text-mint" />Paused in game
                  </div>
                </div>
              </Tilt>
            </Reveal>
          </div>

          <div className="mx-auto mt-24 flex w-[min(1240px,100%-32px)] flex-wrap justify-center gap-3">
            {trust.map((t, i) => (
              <Reveal key={t} delay={0.9 + i * 0.08}>
                <span className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/5 px-4 py-2 text-sm text-mauve"><Check className="size-4 text-mint" />{t}</span>
              </Reveal>
            ))}
          </div>
        </header>

        {/* crossing tapes */}
        <div className="relative z-10 -my-6 py-16" aria-label="Wallpaper effects">
          <Tape items={effects} className="-rotate-3 scale-105 bg-gradient-to-r from-sakura via-peach to-butter text-ink shadow-2xl shadow-sakura/30" />
          <Tape items={effects} reverse className="absolute inset-x-0 top-1/2 -translate-y-1/2 rotate-2 scale-105 border-y border-white/10 bg-ink-2/90 text-cream/70 backdrop-blur" />
        </div>

        {/* FEATURES: pinned horizontal scroll */}
        <HScroll intro={
          <div className="flex shrink-0 flex-col justify-center lg:w-[420px]">
            <div><Eyebrow>Features</Eyebrow></div>
            <h2 className="mt-6 text-[clamp(2.2rem,4.5vw,3.6rem)] font-black">Everything your desktop was <span className="grad-text">missing</span>.</h2>
            <p className="mt-5 text-mauve">Six pages of settings in a futuristic app, all built to stay out of your way when you work or play.</p>
            <p className="mt-8 hidden items-center gap-2 text-sm text-sakura lg:flex">Keep scrolling <ArrowRight className="size-4 animate-pulse" /></p>
          </div>
        }>
          {features.map((f, i) => (
            <SpotCard key={f.img} className="group flex shrink-0 flex-col overflow-hidden rounded-[2rem] border border-white/10 bg-ink-2/70 backdrop-blur-md transition-colors hover:border-sakura/50 lg:h-[68vh] lg:w-[min(500px,60vw)] lg:even:mt-12">
              <div className="relative aspect-[16/10] overflow-hidden bg-gradient-to-br from-plum to-sakura/20 lg:aspect-auto lg:flex-1">
                <Img src={`/images/${f.img}`} alt={f.alt} loading="lazy" className="size-full object-cover transition-transform duration-700 group-hover:scale-110 group-hover:rotate-1" />
                <span className="outline-text absolute top-4 right-5 font-display text-6xl font-black">0{i + 1}</span>
              </div>
              <div className="p-7">
                <span className="text-xs font-semibold tracking-widest text-sakura uppercase">{f.tag}</span>
                <h3 className="mt-2 text-2xl font-bold">{f.title}</h3>
                <p className="mt-3 text-[0.95rem] text-mauve">{f.text}</p>
              </div>
            </SpotCard>
          ))}
        </HScroll>

        {/* SHORTCUTS: live keycaps */}
        <section id="shortcuts" className="py-32">
          <div className="mx-auto grid w-[min(1200px,100%-32px)] items-center gap-16 lg:grid-cols-2">
            <div>
              <Reveal><Eyebrow>Keyboard first</Eyebrow></Reveal>
              <Reveal delay={0.1}><h2 className="mt-6 text-[clamp(2.2rem,4.5vw,3.6rem)] font-black">Control it <span className="grad-text">without looking</span>.</h2></Reveal>
              <Reveal delay={0.2}>
                <p className="mt-6 max-w-md text-mauve">Every shortcut is <kbd className="rounded-md border border-white/20 bg-white/10 px-2 py-0.5 text-sm text-cream">Ctrl</kbd> + <kbd className="rounded-md border border-white/20 bg-white/10 px-2 py-0.5 text-sm text-cream">Alt</kbd> + a letter. Go on, try one on this page.</p>
              </Reveal>
            </div>
            <Keycaps keys={shortcuts} />
          </div>
        </section>

        {/* INSTALL: scroll-filled timeline + parallax download card */}
        <section id="install" className="relative py-32">
          <div className="mx-auto grid w-[min(1200px,100%-32px)] items-center gap-16 lg:grid-cols-[1.1fr_1fr]">
            <div>
              <Reveal><Eyebrow>Installer</Eyebrow></Reveal>
              <Reveal delay={0.1}><h2 className="mt-6 text-[clamp(2.2rem,4.5vw,3.6rem)] font-black">Installed in <span className="grad-text">one click</span>.</h2></Reveal>
              <Reveal delay={0.2}><p className="mt-5 text-lg text-mauve">A two-click installer, no admin password, nothing else to install.</p></Reveal>
              <Timeline>
                {steps.map(([b, s], i) => (
                  <li key={b} className="relative">
                    <span className="absolute top-0 -left-16 grid size-12 place-items-center rounded-2xl bg-gradient-to-br from-sakura to-butter font-display font-black text-ink shadow-lg shadow-sakura/30">{i + 1}</span>
                    <Reveal delay={i * 0.1} y={16}>
                      <b className="block font-display text-lg">{b}</b>
                      <span className="text-mauve">{s}</span>
                    </Reveal>
                  </li>
                ))}
              </Timeline>
            </div>

            <Parallax speed={0.12}>
              <SpotCard className="rounded-[2rem] border border-white/15 bg-gradient-to-br from-plum/80 to-ink-2/80 p-8 shadow-2xl shadow-sakura/10 backdrop-blur-xl">
                <div className="mb-7 aspect-[4/3] overflow-hidden rounded-2xl bg-ink">
                  <Img src="/images/installer.webp" alt="Astra Desk on a Windows 11 desktop" loading="lazy" className="size-full object-cover" />
                </div>
                <a className="btn btn-primary w-full" href={DOWNLOAD_URL} download><Download className="size-5" />Download installer</a>
                <dl className="mt-6 grid grid-cols-2 gap-x-5 gap-y-3 text-sm text-mauve">
                  {[["Version", VERSION], ["Size", SIZE], ["System", "Windows 11"], ["Rights", "No admin needed"]].map(([k, v]) => (
                    <div key={k}><dt className="font-medium text-cream">{k}</dt><dd>{v}</dd></div>
                  ))}
                </dl>
              </SpotCard>
            </Parallax>
          </div>
        </section>

        {/* FAQ: sticky heading + list */}
        <section id="faq" className="py-32">
          <div className="mx-auto grid w-[min(1200px,100%-32px)] gap-12 lg:grid-cols-[1fr_1.6fr]">
            <div className="lg:sticky lg:top-32 lg:self-start">
              <Reveal><Eyebrow>FAQ</Eyebrow></Reveal>
              <Reveal delay={0.1}><h2 className="mt-6 text-[clamp(2.2rem,4.5vw,3.6rem)] font-black">Good to <span className="grad-text">know</span>.</h2></Reveal>
            </div>
            <div className="grid gap-3">
              {faq.map(([q, a], i) => (
                <Reveal key={q} delay={i * 0.05}>
                  <details className="group rounded-2xl border border-white/10 bg-white/[0.03] px-6 py-5 transition-colors open:border-sakura/40 open:bg-sakura/5">
                    <summary className="flex cursor-pointer list-none items-center justify-between gap-4 font-semibold">
                      {q}
                      <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-full bg-white/10 text-sakura transition-transform duration-300 group-open:rotate-45">+</span>
                    </summary>
                    <p className="mt-3 text-mauve">{a}</p>
                  </details>
                </Reveal>
              ))}
            </div>
          </div>
        </section>

        {/* FINAL CTA: giant outlined marquee behind */}
        <section className="relative overflow-hidden py-40 text-center">
          <div aria-hidden="true" className="absolute inset-x-0 top-1/2 -translate-y-1/2 opacity-60">
            <div className="animate-marquee flex w-max gap-16">
              {Array.from({ length: 6 }, (_, i) => (
                <span key={i} className="outline-text font-display text-[clamp(5rem,14vw,12rem)] font-black whitespace-nowrap">MAKE IT YOURS ✿</span>
              ))}
            </div>
          </div>
          <div className="relative mx-auto w-[min(1200px,100%-32px)]">
            <h2 className="text-[clamp(2.6rem,7vw,5.5rem)] font-black">
              Make Windows 11<br /><span className="grad-text">yours.</span>
            </h2>
            <Reveal delay={0.2} className="mt-10 flex flex-wrap justify-center gap-4">
              <Magnetic><a className="btn btn-primary" href={DOWNLOAD_URL} download><Zap className="size-5" />Download for Windows</a></Magnetic>
            </Reveal>
            <p className="mt-5 text-sm text-mauve"><b className="font-medium text-cream">v{VERSION}</b> · {SIZE} · free</p>
          </div>
        </section>
      </main>
      <DonatePopup url={KOFI_URL} />

      <footer className="border-t border-white/10 py-9 text-sm text-mauve">
        <div className="mx-auto flex w-[min(1200px,100%-32px)] flex-wrap justify-between gap-4">
          <span>© 2026 Astra Desk</span>
          <nav aria-label="Guides" className="flex flex-wrap gap-x-6 gap-y-2">
            {guides.map((g) => <a key={g.slug} href={`/${g.slug}`} className="hover:text-cream">{g.nav}</a>)}
          </nav>
          <span>Not affiliated with Microsoft. Windows is a trademark of Microsoft Corporation.</span>
        </div>
      </footer>
    </>
  );
}
