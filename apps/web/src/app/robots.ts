import type { MetadataRoute } from "next";
import { getSiteUrl, isIndexable } from "@/server/config";

export const dynamic = "force-dynamic";

export default function robots(): MetadataRoute.Robots {
  const site = getSiteUrl();
  // Only the production server is indexable (L-45, D301): staging, the demo and dev servers ask crawlers away.
  if (!isIndexable() || !site) return { rules: { userAgent: "*", disallow: "/" } };
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/api/",
        "/bff/",
        "/cart",
        "/checkout",
        "/login",
        "/account",
        "/orders",
        "/search",
      ],
    },
    sitemap: `${site}/sitemap.xml`,
  };
}
