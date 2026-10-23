import type { Metadata } from "next";
import Link from "next/link";
import { getCategories } from "@/api/endpoints/catalog";
import { publicHttp, requestApi } from "@/api/server";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { Container } from "@/components/section";
import { ProductListing } from "@/components/shop/product-listing";
import { parseShopFilters } from "@/lib/shop-params";
import { settle } from "@/lib/settle";

export async function generateMetadata(props: PageProps<"/search">): Promise<Metadata> {
  const { q } = parseShopFilters(await props.searchParams);
  return {
    title: q ? `Search results for “${q}”` : "Search",
    // Search result pages are not useful in search engines.
    robots: { index: false, follow: true },
  };
}

export default async function SearchPage(props: PageProps<"/search">) {
  const searchParams = await props.searchParams;
  const filters = parseShopFilters(searchParams);
  const crumbs = [{ href: "/", label: "Home" }, { label: "Search" }];

  if (!filters.q || filters.q.length < 2) {
    const categories = await settle(getCategories(publicHttp(300)));
    return (
      <Container className="flex flex-col gap-6 py-8">
        <Breadcrumbs items={crumbs} />
        <h1 className="text-text text-3xl font-bold">Search</h1>
        <p className="text-text-muted">
          Type at least two letters in the search box above. Local names work too, for example
          “vanjaram” or “eral”.
        </p>
        {categories.ok && categories.value.length > 0 && (
          <ul className="flex flex-wrap gap-2" aria-label="Browse categories">
            {categories.value.map((c) => (
              <li key={c.id}>
                <Link
                  href={`/shop/${c.slug}`}
                  className="border-border bg-surface hover:bg-muted inline-flex rounded-full border px-4 py-1.5 text-sm font-medium"
                >
                  {c.name}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Container>
    );
  }

  const api = await requestApi();
  return (
    <Container className="flex flex-col gap-6 py-8">
      <Breadcrumbs items={crumbs} />
      <h1 className="text-text text-3xl font-bold">Results for “{filters.q}”</h1>
      <ProductListing api={api} filters={filters} searchParams={searchParams} basePath="/search" />
    </Container>
  );
}
