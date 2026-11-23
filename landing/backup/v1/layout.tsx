import type { Metadata, Viewport } from "next";
import { Inter, Space_Grotesk } from "next/font/google";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-body" });
const grotesk = Space_Grotesk({ subsets: ["latin"], weight: ["500", "700"], variable: "--font-head" });

const title = "Desktop Styler: make Windows 11 yours";
const description =
  "Transparent custom taskbar, animated anime wallpapers with effects, desktop clock, screensaver and one-click window layouts. Free for Windows 11.";

export const metadata: Metadata = {
  // set SITE_URL to the real domain when deploying, so share previews get absolute image URLs
  metadataBase: new URL(process.env.SITE_URL ?? "http://localhost:3000"),
  title,
  description,
  openGraph: { title, description, images: ["/images/og-image.png"] },
  twitter: { card: "summary_large_image" },
};

export const viewport: Viewport = { themeColor: "#05070d" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${inter.variable} ${grotesk.variable}`} suppressHydrationWarning>
      <head>
        {/* marks JS as available before paint, so .reveal content stays visible without JS */}
        <script dangerouslySetInnerHTML={{ __html: "document.documentElement.classList.add('js')" }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
