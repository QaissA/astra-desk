import type { Metadata, Viewport } from "next";
import { Inter, Unbounded } from "next/font/google";
import { Providers } from "./client";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-body" });
const display = Unbounded({ subsets: ["latin"], weight: ["500", "700", "900"], variable: "--font-head" });

const title = "Astra Desk: make Windows 11 yours";
const description =
  "Transparent custom taskbar, animated anime wallpapers with effects, desktop clock, screensaver and one-click window layouts. Free for Windows 11.";

export const metadata: Metadata = {
  // production domain, so share previews get absolute image URLs (SITE_URL overrides it)
  metadataBase: new URL(process.env.SITE_URL ?? "https://www.astra-desk.xyz"),
  title,
  description,
  openGraph: { title, description, images: ["/images/og-image.png"] },
  twitter: { card: "summary_large_image" },
};

export const viewport: Viewport = { themeColor: "#110a1c" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${inter.variable} ${display.variable}`} suppressHydrationWarning>
      <body className="noise"><Providers>{children}</Providers></body>
    </html>
  );
}
