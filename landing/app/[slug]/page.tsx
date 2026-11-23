import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { guides } from "../guides";

export const dynamicParams = false; // only the guides exist; anything else 404s

export function generateStaticParams() {
  return guides.map((g) => ({ slug: g.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const g = guides.find((x) => x.slug === slug);
  if (!g) return {};
  return {
    title: g.title,
    description: g.description,
    alternates: { canonical: `/${g.slug}` },
    openGraph: { title: g.title, description: g.description, images: ["/images/og-image.png"], type: "article" },
  };
}

export default async function GuidePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const g = guides.find((x) => x.slug === slug);
  if (!g) notFound();
  const faqLd = {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: g.faq.map(([q, a]) => ({ "@type": "Question", name: q, acceptedAnswer: { "@type": "Answer", text: a } })),
  };

  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqLd) }} />
      <nav className="mx-auto flex w-[min(820px,100%-32px)] items-center justify-between py-6">
        <a href="/" className="flex items-center gap-2.5 font-display text-sm font-bold">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/images/logo-small.png" alt="" className="size-7 rounded-lg" />Astra Desk
        </a>
        <a className="btn btn-primary px-5! py-2.5! text-sm" href="/downloads/AstraDesk-Setup.exe" download>Download free</a>
      </nav>

      <main className="mx-auto w-[min(820px,100%-32px)] pt-10 pb-24">
        <span className="chip">Guide</span>
        <h1 className="mt-6 font-display text-[clamp(2rem,5vw,3.2rem)] leading-tight font-black">{g.h1}</h1>
        <p className="mt-6 text-lg text-mauve">{g.intro}</p>

        {g.sections.map((s) => (
          <section key={s.h2} className="mt-14">
            <h2 className="font-display text-2xl font-bold">{s.h2}</h2>
            {s.body.map((p) => <p key={p} className="mt-4 text-mauve">{p}</p>)}
            {s.steps && (
              <ol className="mt-5 grid list-decimal gap-3 pl-6 text-mauve marker:font-bold marker:text-sakura">
                {s.steps.map((st) => <li key={st}>{st}</li>)}
              </ol>
            )}
          </section>
        ))}

        <section className="mt-14 rounded-3xl border border-white/10 bg-white/[0.03] p-8 text-center">
          <h2 className="font-display text-2xl font-bold">Try Astra Desk, free</h2>
          <p className="mt-3 text-mauve">Animated wallpapers, a transparent custom taskbar, a desktop clock and more for Windows 11. No ads, no account.</p>
          <a className="btn btn-primary mt-6" href="/downloads/AstraDesk-Setup.exe" download>Download for Windows</a>
        </section>

        <section className="mt-14">
          <h2 className="font-display text-2xl font-bold">FAQ</h2>
          {g.faq.map(([q, a]) => (
            <div key={q} className="mt-5">
              <h3 className="font-semibold">{q}</h3>
              <p className="mt-1 text-mauve">{a}</p>
            </div>
          ))}
        </section>

        <section className="mt-14 border-t border-white/10 pt-8">
          <h2 className="font-display text-lg font-bold">More guides</h2>
          <ul className="mt-3 grid gap-2">
            {guides.filter((x) => x.slug !== g.slug).map((x) => (
              <li key={x.slug}><a href={`/${x.slug}`} className="text-sakura hover:underline">{x.h1}</a></li>
            ))}
            <li><a href="/" className="text-sakura hover:underline">Astra Desk home</a></li>
          </ul>
        </section>
      </main>
    </>
  );
}
