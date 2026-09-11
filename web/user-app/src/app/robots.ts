import type { MetadataRoute } from "next";

// This tells search engines which pages of Natoshare they are allowed to look at.
// The dashboard and anything else that needs a login should never be indexed, so we
// keep those out here.
export default function robots(): MetadataRoute.Robots {
  const baseUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://natoshare.example.com";

  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
        disallow: [
          "/dashboard",
          "/onboarding",
          "/settings",
          "/reports",
          "/categories",
          "/income",
          "/expenses",
        ],
      },
    ],
    sitemap: `${baseUrl}/sitemap.xml`,
  };
}
