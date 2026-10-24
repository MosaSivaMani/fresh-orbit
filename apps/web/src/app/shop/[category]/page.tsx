import { isApiError } from "@fresh/core";
import { FilterChip, PageBand } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";
import { getCategories, getCategory } from "@/api/endpoints/catalog";
import { publicHttp, requestApi } from "@/api/server";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { PageSearch } from "@/components/header/page-search";
import { Container } from "@/components/section";
import { ProductListing } from "@/components/shop/product-listing";
import { parseShopFilters } from "@/lib/shop-params";
import { settle } from "@/lib/settle";

const loadCategory = cache(async (slug: string) => {
  try {
    return await getCategory(publicHttp(300), slug);
  } catch (error) {
    if (isApiError(error) && error.status === 404) return null;
    throw error;
  }
});

export async function generateMetadata(props: PageProps<"/shop/[category]">): Promise<Metadata> {
  const { category } = await props.params;
  const detail = await loadCategory(category).catch(() => null);
  if (!detail) return { title: "Category not found" };
  return {
    title: detail.name,
    description: detail.description ?? `Shop ${detail.name.toLowerCase()} at Fresh Orbitt.`,
    alternates: { canonical: `/shop/${detail.slug}` },
  };
}

export default async function CategoryPage(props: PageProps<"/shop/[category]">) {
  const { category } = await props.params;
  const searchParams = await props.searchParams;
  const [detail, api, roots] = await Promise.all([
    loadCategory(category),
    requestApi(),
    settle(getCategories(publicHttp(300))),
  ]);
  if (!detail) notFound();
  const filters = parseShopFilters(searchParams, detail.slug);
  return (
    <>
      <PageBand
        eyebrow={
          <Breadcrumbs
            tone="band"
            items={[
              { href: "/", label: "Home" },
              { href: "/shop", label: "Shop" },
              ...(detail.parent
                ? [{ href: `/shop/${detail.parent.slug}`, label: detail.parent.name }]
                : []),
              { label: detail.name },
            ]}
          />
        }
        title={detail.name}
        description={detail.description ?? undefined}
      >
        <PageSearch />
      </PageBand>
      <Container className="flex flex-col gap-5 py-6 sm:py-8">
        {detail.children.length > 0 && (
          <ul className="flex flex-wrap gap-2" aria-label="Sub-categories">
            {detail.children.map((child) => (
              <li key={child.id}>
                <FilterChip asChild>
                  <Link href={`/shop/${child.slug}`}>{child.name}</Link>
                </FilterChip>
              </li>
            ))}
          </ul>
        )}
        <ProductListing
          api={api}
          filters={filters}
          searchParams={searchParams}
          basePath={`/shop/${detail.slug}`}
          navCategories={roots.ok ? roots.value : []}
          currentCategory={detail.parent?.slug ?? detail.slug}
          offerCategoryIds={[
            detail.id,
            ...(roots.ok && detail.parent
              ? roots.value.filter((c) => c.slug === detail.parent?.slug).map((c) => c.id)
              : []),
          ]}
        />
      </Container>
    </>
  );
}
