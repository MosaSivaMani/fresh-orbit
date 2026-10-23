import type { MetadataRoute } from "next";
import { getCategories, listProducts } from "@/api/endpoints/catalog";
import { publicHttp } from "@/api/server";
import { type CategoryNode } from "@/api/types/catalog";
import { getSiteUrl } from "@/server/config";

// Built per request (and cached for an hour by the catalogue fetches) so a build never needs the API.
export const dynamic = "force-dynamic";

const STATIC_PATHS = [
  "/",
  "/shop",
  "/about",
  "/support",
  "/support/delivery-policy",
  "/support/cancellation-policy",
  "/support/refund-policy",
];
const PRODUCT_PAGE_SIZE = 100;
const MAX_PRODUCT_PAGES = 20;

function flatten(nodes: CategoryNode[]): CategoryNode[] {
  return nodes.flatMap((n) => [n, ...flatten(n.children)]);
}

/** Public catalogue pages only; customer pages (cart, checkout, account, orders) are never listed. */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const site = getSiteUrl();
  // Sitemaps need absolute URLs; without a configured site URL there is nothing correct to publish.
  if (!site) return [];
  const entries: MetadataRoute.Sitemap = STATIC_PATHS.map((path) => ({ url: `${site}${path}` }));

  try {
    const http = publicHttp(3600);
    const categories = flatten(await getCategories(http));
    entries.push(...categories.map((c) => ({ url: `${site}/shop/${c.slug}` })));

    for (let page = 1; page <= MAX_PRODUCT_PAGES; page++) {
      const result = await listProducts(http, { page, pageSize: PRODUCT_PAGE_SIZE });
      entries.push(...result.items.map((p) => ({ url: `${site}/products/${p.slug}` })));
      if (page * result.pageSize >= result.totalCount) break;
    }
  } catch (error) {
    // The catalogue part is best effort; static pages are still listed.
    console.error("sitemap: catalogue unavailable", error);
  }
  return entries;
}
