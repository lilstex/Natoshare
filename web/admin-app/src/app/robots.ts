import type { MetadataRoute } from "next";

// The admin app is private, it should never show up in a search engine.
// This blocks every crawler from every single page, no exceptions.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        disallow: "/",
      },
    ],
  };
}
