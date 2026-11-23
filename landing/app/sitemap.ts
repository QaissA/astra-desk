import type { MetadataRoute } from "next";
import { guides } from "./guides";

const base = "https://www.astra-desk.xyz";

export default function sitemap(): MetadataRoute.Sitemap {
  return [
    { url: base, lastModified: new Date(), priority: 1 },
    ...guides.map((g) => ({ url: `${base}/${g.slug}`, lastModified: new Date(), priority: 0.8 })),
  ];
}
