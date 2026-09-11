import type { MetadataRoute } from "next";

// This lists the public pages of Natoshare for search engines to find.
// Right now that is only the home page. We will add pricing and other public pages
// here as we build them in later phases.
export default function sitemap(): MetadataRoute.Sitemap {
  const baseUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://natoshare.example.com";

  return [
    {
      url: baseUrl,
      lastModified: new Date(),
      changeFrequency: "weekly",
      priority: 1,
    },
  ];
}
