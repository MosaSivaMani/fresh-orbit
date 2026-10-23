import type { MetadataRoute } from "next";
import { getSiteUrl, isProduction } from "@/server/config";

export const dynamic = "force-dynamic";

export default function robots(): MetadataRoute.Robots {
  const site = getSiteUrl();
  // Only a production deployment with a configured public URL is indexable.
  if (!isProduction || !site) return { rules: { userAgent: "*", disallow: "/" } };
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
