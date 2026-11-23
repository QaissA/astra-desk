"use client";
import {
  AnimatePresence, MotionConfig, motion, useMotionValue, useReducedMotion, useScroll, useSpring, useTransform,
} from "motion/react";
import { Analytics } from "@vercel/analytics/next";
import { pageview } from "@vercel/analytics";
import { useEffect, useRef, useState, type ImgHTMLAttributes, type ReactNode } from "react";

// Honour the OS "reduce motion" setting for every motion component below.
export function Providers({ children }: { children: ReactNode }) {
  useEffect(() => {
    // free-plan analytics has no custom events, so each download click is counted as a view of "/download"
    const onClick = (e: MouseEvent) => { if ((e.target as Element).closest?.("a[download]")) pageview({ path: "/download" }); };
    document.addEventListener("click", onClick);
    return () => document.removeEventListener("click", onClick);
  }, []);
  return <MotionConfig reducedMotion="user"><Analytics />{children}</MotionConfig>;
}

// Fade, lift and unblur into view once.
export function Reveal({ children, delay = 0, className, y = 32 }: { children: ReactNode; delay?: number; className?: string; y?: number }) {
  return (
    <motion.div className={className} initial={{ opacity: 0, y, filter: "blur(8px)" }}
      whileInView={{ opacity: 1, y: 0, filter: "blur(0px)" }} viewport={{ once: true, amount: 0.2 }}
      transition={{ duration: 0.8, delay, ease: [0.22, 1, 0.36, 1] }}>
      {children}
    </motion.div>
  );
}

// Headline words that drop in one after another.
export function SplitWords({ text, className, delay = 0 }: { text: string; className?: string; delay?: number }) {
  return (
    <span className={className}>
      {text.split(" ").map((w, i) => (
        <span key={i} className="inline-block overflow-hidden pb-[0.12em] align-bottom">
          <motion.span className="inline-block" initial={{ y: "110%", rotate: 8 }} animate={{ y: 0, rotate: 0 }}
            transition={{ duration: 0.9, delay: delay + i * 0.08, ease: [0.22, 1, 0.36, 1] }}>
            {w}&nbsp;
          </motion.span>
        </span>
      ))}
    </span>
  );
}

export function RotatingWord({ words }: { words: string[] }) {
  const [i, setI] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setI((n) => (n + 1) % words.length), 2400);
    return () => clearInterval(id);
  }, [words.length]);
  return (
    <span className="relative inline-grid overflow-hidden align-bottom pb-[0.1em]" aria-live="polite">
      <AnimatePresence mode="popLayout" initial={false}>
        <motion.span key={words[i]} className="grad-text col-start-1 row-start-1"
          initial={{ y: "100%", opacity: 0, rotateX: -80 }} animate={{ y: 0, opacity: 1, rotateX: 0 }}
          exit={{ y: "-100%", opacity: 0, rotateX: 80 }} transition={{ duration: 0.6, ease: [0.22, 1, 0.36, 1] }}>
          {words[i]}
        </motion.span>
      </AnimatePresence>
    </span>
  );
}

// 3D tilt that follows the pointer, with springs so it settles smoothly.
export function Tilt({ children, className, max = 12 }: { children: ReactNode; className?: string; max?: number }) {
  const ref = useRef<HTMLDivElement>(null);
  const px = useMotionValue(0), py = useMotionValue(0);
  const rotateY = useSpring(useTransform(px, [-0.5, 0.5], [-max, max]), { stiffness: 120, damping: 14 });
  const rotateX = useSpring(useTransform(py, [-0.5, 0.5], [max * 0.7, -max * 0.7]), { stiffness: 120, damping: 14 });
  return (
    <div className="[perspective:1400px]">
      <motion.div ref={ref} className={className} style={{ rotateX, rotateY, transformStyle: "preserve-3d" }}
        onPointerMove={(e) => {
          const r = ref.current!.getBoundingClientRect();
          px.set((e.clientX - r.left) / r.width - 0.5);
          py.set((e.clientY - r.top) / r.height - 0.5);
        }}
        onPointerLeave={() => { px.set(0); py.set(0); }}>
        {children}
      </motion.div>
    </div>
  );
}

// Moves with scroll at a different speed than the page.
export function Parallax({ children, className, speed = 0.2 }: { children: ReactNode; className?: string; speed?: number }) {
  const ref = useRef<HTMLDivElement>(null);
  const { scrollYProgress } = useScroll({ target: ref, offset: ["start end", "end start"] });
  const y = useTransform(scrollYProgress, [0, 1], [`${speed * 100}%`, `${-speed * 100}%`]);
  return <motion.div ref={ref} className={className} style={{ y }}>{children}</motion.div>;
}

// A button that leans toward the pointer.
export function Magnetic({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const x = useSpring(0, { stiffness: 200, damping: 12 }), y = useSpring(0, { stiffness: 200, damping: 12 });
  return (
    <motion.div ref={ref} className="inline-block" style={{ x, y }}
      onPointerMove={(e) => {
        const r = ref.current!.getBoundingClientRect();
        x.set((e.clientX - r.left - r.width / 2) * 0.3);
        y.set((e.clientY - r.top - r.height / 2) * 0.4);
      }}
      onPointerLeave={() => { x.set(0); y.set(0); }}>
      {children}
    </motion.div>
  );
}

// Card with a glow that follows the pointer (reads --x / --y in CSS).
export function SpotCard({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={`spot ${className ?? ""}`}
      onPointerMove={(e) => {
        const r = e.currentTarget.getBoundingClientRect();
        e.currentTarget.style.setProperty("--x", `${e.clientX - r.left}px`);
        e.currentTarget.style.setProperty("--y", `${e.clientY - r.top}px`);
      }}>
      {children}
    </div>
  );
}

// Pinned section whose track slides sideways as you scroll down. Plain vertical stack on small screens.
export function HScroll({ intro, children }: { intro: ReactNode; children: ReactNode }) {
  const section = useRef<HTMLElement>(null), track = useRef<HTMLDivElement>(null);
  const reduce = useReducedMotion();
  const [on, setOn] = useState(false), [dist, setDist] = useState(0);
  useEffect(() => {
    // when `on` flips, the track re-lays out as a row and the ResizeObserver measures again
    const measure = () => {
      const wide = innerWidth > 1024 && !reduce;
      setOn(wide);
      setDist(wide ? Math.max(0, track.current!.scrollWidth - innerWidth) : 0);
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(track.current!);
    addEventListener("resize", measure);
    return () => { ro.disconnect(); removeEventListener("resize", measure); };
  }, [reduce]);
  const { scrollYProgress } = useScroll({ target: section, offset: ["start start", "end end"] });
  const x = useTransform(scrollYProgress, (p) => -p * dist);
  const bar = useTransform(scrollYProgress, [0, 1], [0, 1]);
  return (
    <section ref={section} id="features" className="relative" style={dist ? { height: `calc(${dist}px + 100vh)` } : undefined}>
      <div className={dist ? "sticky top-0 flex h-screen flex-col justify-center overflow-hidden pt-16" : "py-24"}>
        <motion.div ref={track} style={dist ? { x } : undefined}
          className={on ? "flex w-max items-stretch gap-6 pr-16 pl-[max(2rem,calc((100vw-1200px)/2))]"
            : "mx-auto grid w-[min(1200px,100%-32px)] gap-6 sm:grid-cols-2 [&>*:first-child]:col-span-full"}>
          {intro}
          {children}
        </motion.div>
        {dist > 0 && (
          <div className="mx-auto mt-10 h-1 w-64 overflow-hidden rounded-full bg-white/10">
            <motion.div className="h-full origin-left bg-gradient-to-r from-sakura via-peach to-butter" style={{ scaleX: bar }} />
          </div>
        )}
      </div>
    </section>
  );
}

// Keycaps that press down when you hit the real shortcut.
export function Keycaps({ keys }: { keys: [string, string][] }) {
  const [hit, setHit] = useState<string | null>(null);
  useEffect(() => {
    let t: ReturnType<typeof setTimeout>;
    const onKey = (e: KeyboardEvent) => {
      const k = e.key.toUpperCase();
      if (!e.ctrlKey || !e.altKey || !keys.some(([, key]) => key === k)) return;
      setHit(k);
      clearTimeout(t);
      t = setTimeout(() => setHit(null), 500);
    };
    addEventListener("keydown", onKey);
    return () => { removeEventListener("keydown", onKey); clearTimeout(t); };
  }, [keys]);
  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
      {keys.map(([label, k], i) => (
        <motion.div key={k} initial={{ opacity: 0, y: 40, rotate: i % 2 ? 6 : -6 }}
          whileInView={{ opacity: 1, y: 0, rotate: 0 }} viewport={{ once: true }}
          transition={{ type: "spring", stiffness: 160, damping: 14, delay: i * 0.07 }}
          className="flex flex-col items-center gap-3">
          <motion.div whileHover={{ y: -4 }} whileTap={{ y: 6 }} animate={hit === k ? { y: [0, 8, 0] } : undefined}
            className={`keycap ${hit === k ? "keycap-hit" : ""}`}>{k}</motion.div>
          <span className="text-sm text-mauve">{label}</span>
        </motion.div>
      ))}
    </div>
  );
}

// Vertical line that fills as the steps scroll past.
export function Timeline({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLOListElement>(null);
  const { scrollYProgress } = useScroll({ target: ref, offset: ["start 75%", "end 50%"] });
  const scaleY = useSpring(scrollYProgress, { stiffness: 90, damping: 20 });
  return (
    <ol ref={ref} className="relative my-10 grid list-none gap-10 p-0 pl-16">
      <span className="absolute top-2 bottom-2 left-[23px] w-[3px] rounded bg-white/10" aria-hidden="true" />
      <motion.span className="absolute top-2 bottom-2 left-[23px] w-[3px] origin-top rounded bg-gradient-to-b from-sakura via-peach to-butter"
        style={{ scaleY }} aria-hidden="true" />
      {children}
    </ol>
  );
}

// Thank-you popup with the donation link, opened by any download button (<a download>) once the download has started.
export function DonatePopup({ url }: { url: string }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    let t: ReturnType<typeof setTimeout>;
    const onClick = (e: MouseEvent) => {
      if (!(e.target as Element).closest?.("a[download]")) return;
      clearTimeout(t);
      t = setTimeout(() => { if (!ref.current?.open) ref.current?.showModal(); }, 600);
    };
    document.addEventListener("click", onClick);
    return () => { document.removeEventListener("click", onClick); clearTimeout(t); };
  }, []);
  return (
    <dialog ref={ref} aria-labelledby="donate-title"
      onClick={(e) => { if (e.target === e.currentTarget) e.currentTarget.close(); }} // click on the backdrop closes
      className="m-auto w-[min(30rem,100%-32px)] overflow-hidden rounded-[2rem] border border-white/10 bg-ink-2 p-0 text-center text-cream shadow-[0_0_80px_-20px_var(--color-sakura)] backdrop:bg-black/70 backdrop:backdrop-blur-sm">
      <div className="relative isolate px-8 py-10">
        <div aria-hidden="true" className="absolute inset-0 -z-10 opacity-25"><Img src="/images/donate-bg.webp" alt="" className="size-full object-cover" /></div>
        <button onClick={() => ref.current?.close()} aria-label="Close"
          className="absolute top-4 right-4 grid size-9 place-items-center rounded-full bg-white/10 text-lg hover:bg-white/20">×</button>
        <p className="text-xs font-semibold tracking-widest text-sakura uppercase">Your download has started</p>
        <h2 id="donate-title" className="mt-4 text-3xl font-black">Free forever,<br /><span className="grad-text">powered by you</span>.</h2>
        <p className="mt-4 text-mauve">Astra Desk has no ads and no paid tier. If it makes your desktop better, a coffee keeps new features and wallpaper effects coming.</p>
        <a className="btn btn-primary mt-8 w-full" href={url} target="_blank" rel="noopener noreferrer">☕ Support on Ko-fi</a>
        <p className="mt-4 text-xs text-mauve">From $5, one time or monthly, by PayPal or card.</p>
      </div>
    </dialog>
  );
}

// Hides itself when the file is missing, so the gradient placeholder shows until the image is added.
export function Img(props: ImgHTMLAttributes<HTMLImageElement>) {
  const ref = useRef<HTMLImageElement>(null);
  useEffect(() => {
    const img = ref.current;
    if (img?.complete && img.naturalWidth === 0) img.hidden = true; // error fired before hydration
  }, []);
  // phones get the 800px copy (public/images/*-800.webp, made with sharp); 1600w is nominal for the originals
  const src = typeof props.src === "string" && /^\/images\/[\w-]+\.webp$/.test(props.src) ? props.src : null;
  const responsive = src ? { srcSet: `${src.replace(".webp", "-800.webp")} 800w, ${src} 1600w`, sizes: "(min-width: 1024px) 50vw, 100vw" } : {};
  // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
  return <img ref={ref} {...responsive} {...props} onError={(e) => { e.currentTarget.hidden = true; }} />;
}
