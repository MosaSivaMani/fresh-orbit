import { Heading, Script } from "@fresh/ui";
import type { Metadata } from "next";
import { getCategories } from "@/api/endpoints/catalog";
import { publicHttp, requestApi } from "@/api/server";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { PageSearch } from "@/components/header/page-search";
import { Container } from "@/components/section";
import { ProductListing } from "@/components/shop/product-listing";
import { parseShopFilters } from "@/lib/shop-params";
import { settle } from "@/lib/settle";

export const metadata: Metadata = {
  title: "Shop fresh fish and seafood",
  description:
    "Browse fresh fish, prawns, crabs and shellfish. Filter by category, source and stock.",
  alternates: { canonical: "/shop" },
};

export default async function ShopPage(props: PageProps<"/shop">) {
  const searchParams = await props.searchParams;
  const [api, categories] = await Promise.all([
    requestApi(),
    settle(getCategories(publicHttp(300))),
  ]);
  const roots = categories.ok ? categories.value : [];
  const filters = parseShopFilters(searchParams);
  return (
    <Container className="flex flex-col gap-5 py-6 sm:py-8">
      <Breadcrumbs items={[{ href: "/", label: "Home" }, { label: "Shop" }]} />
      <Heading level={1} size="page" className="text-text">
        Shop <Script tone="accent">fresh</Script>
      </Heading>
      <PageSearch />
      <ProductListing
        api={api}
        filters={filters}
        searchParams={searchParams}
        basePath="/shop"
        filterCategories={roots.map((c) => ({ slug: c.slug, name: c.name }))}
        categoryNames={Object.fromEntries(roots.map((c) => [c.slug, c.name]))}
        navCategories={roots}
      />
    </Container>
  );
}
