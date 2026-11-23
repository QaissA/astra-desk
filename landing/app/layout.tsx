import type { Metadata, Viewport } from "next";
import { Inter, Unbounded } from "next/font/google";
import { Providers } from "./client";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-body" });
const display = Unbounded({ subsets: ["latin"], weight: ["500", "700", "900"], variable: "--font-head" });

const title = "Astra Desk: Free Windows 11 Customization App, Animated Wallpapers & Custom Taskbar";
const description =
  "Transparent custom taskbar, animated anime wallpapers with effects, desktop clock, screensaver and one-click window layouts. Free for Windows 11.";

export const metadata: Metadata = {
  // production domain, so share previews get absolute image URLs (SITE_URL overrides it)
  metadataBase: new URL(process.env.SITE_URL ?? "https://www.astra-desk.xyz"),
  title,
  description,
  alternates: { canonical: "/" },
  keywords: ["Astra Desk", "Windows 11 customization", "animated wallpaper", "anime wallpaper engine", "transparent taskbar", "custom taskbar", "desktop clock", "live wallpaper Windows 11"],
  openGraph: { title, description, images: ["/images/og-image.png"] },
  twitter: { card: "summary_large_image" },
};

// structured data so Google can show it as a free Windows app
const jsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Astra Desk",
  operatingSystem: "Windows 11",
  applicationCategory: "DesktopEnhancementApplication",
  description,
  url: "https://www.astra-desk.xyz",
  downloadUrl: "https://www.astra-desk.xyz/downloads/AstraDesk-Setup.exe",
  softwareVersion: "1.1.0",
  image: "https://www.astra-desk.xyz/images/og-image.png",
  offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
};

export const viewport: Viewport = { themeColor: "#110a1c" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${inter.variable} ${display.variable}`} suppressHydrationWarning>
      <body className="noise">
        <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
        <Providers>{children}</Providers></body>
    </html>
  );
}
