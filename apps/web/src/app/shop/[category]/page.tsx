import { isApiError } from "@fresh/core";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";
import { getCategory } from "@/api/endpoints/catalog";
import { publicHttp, requestApi } from "@/api/server";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { Container } from "@/components/section";
import { ProductListing } from "@/components/shop/product-listing";
import { parseShopFilters } from "@/lib/shop-params";

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
    description: detail.description ?? `Shop ${detail.name.toLowerCase()} at Fresh Orbit.`,
    alternates: { canonical: `/shop/${detail.slug}` },
  };
}

export default async function CategoryPage(props: PageProps<"/shop/[category]">) {
  const { category } = await props.params;
  const searchParams = await props.searchParams;
  const [detail, api] = await Promise.all([loadCategory(category), requestApi()]);
  if (!detail) notFound();
  const filters = parseShopFilters(searchParams, detail.slug);
  return (
    <Container className="flex flex-col gap-6 py-8">
      <Breadcrumbs
        items={[
          { href: "/", label: "Home" },
          { href: "/shop", label: "Shop" },
          ...(detail.parent
            ? [{ href: `/shop/${detail.parent.slug}`, label: detail.parent.name }]
            : []),
          { label: detail.name },
        ]}
      />
      <div className="flex flex-col gap-2">
        <h1 className="text-text text-3xl font-bold sm:text-4xl">{detail.name}</h1>
        {detail.description && <p className="text-text-muted max-w-2xl">{detail.description}</p>}
      </div>
      {detail.children.length > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Sub-categories">
          {detail.children.map((child) => (
            <li key={child.id}>
              <Link
                href={`/shop/${child.slug}`}
                className="border-border bg-surface text-text hover:bg-muted inline-flex rounded-full border px-4 py-1.5 text-sm font-medium"
              >
                {child.name}
              </Link>
            </li>
          ))}
        </ul>
      )}
      <ProductListing
        api={api}
        filters={filters}
        searchParams={searchParams}
        basePath={`/shop/${detail.slug}`}
      />
    </Container>
  );
}
