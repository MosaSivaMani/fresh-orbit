import { FilterChip } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";
import { getCategories } from "@/api/endpoints/catalog";
import { publicHttp, requestApi } from "@/api/server";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { PageSearch } from "@/components/header/page-search";
import { Container } from "@/components/section";
import { ProductListing } from "@/components/shop/product-listing";
import { Highlight, ThemeBand } from "@/components/theme/theme-band";
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
      <>
        <ThemeBand
          eyebrow={<Breadcrumbs tone="band" items={crumbs} />}
          title={
            <>
              Find your <Highlight>fish</Highlight>
            </>
          }
        >
          <PageSearch />
        </ThemeBand>
        <Container className="flex flex-col gap-6 py-8">
          <p className="text-text-muted">
            Type at least two letters in the search box above. Local names work too, for example
            “vanjaram” or “eral”.
          </p>
          {categories.ok && categories.value.length > 0 && (
            <ul className="flex flex-wrap gap-2" aria-label="Browse categories">
              {categories.value.map((c) => (
                <li key={c.id}>
                  <FilterChip asChild>
                    <Link href={`/shop/${c.slug}`}>{c.name}</Link>
                  </FilterChip>
                </li>
              ))}
            </ul>
          )}
        </Container>
      </>
    );
  }

  const api = await requestApi();
  return (
    <>
      <ThemeBand
        eyebrow={<Breadcrumbs tone="band" items={crumbs} />}
        title={
          <>
            Results for <Highlight>“{filters.q}”</Highlight>
          </>
        }
      >
        <PageSearch />
      </ThemeBand>
      <Container className="py-6 sm:py-8">
        <ProductListing
          api={api}
          filters={filters}
          searchParams={searchParams}
          basePath="/search"
        />
      </Container>
    </>
  );
}
