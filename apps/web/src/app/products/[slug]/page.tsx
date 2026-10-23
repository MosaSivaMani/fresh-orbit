import { formatDayShort, formatRupees, formatTime, isApiError } from "@fresh/core";
import { Badge, Tabs, TabsContent, TabsList, TabsTrigger } from "@fresh/ui";
import { Truck } from "lucide-react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { cache } from "react";
import { getProduct, getServiceability, listProducts } from "@/api/endpoints/catalog";
import { type Located, type RequestApi, loadWithLocation, requestApi } from "@/api/server";
import { type ProductDetail } from "@/api/types/catalog";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { ProductGrid } from "@/components/catalog/product-card";
import { LocationBanner } from "@/components/location-banner";
import { Gallery } from "@/components/product/gallery";
import { PurchasePanel } from "@/components/product/purchase-panel";
import { WishlistButton } from "@/components/product/wishlist-button";
import { Container, Section } from "@/components/section";
import { productJsonLd, serializeJsonLd } from "@/lib/json-ld";
import { settle } from "@/lib/settle";
import { sourceTypeLabels } from "@/lib/status-maps";
import { getSiteUrl } from "@/server/config";

const loadProduct = cache(
  async (api: RequestApi, slug: string): Promise<Located<ProductDetail> | null> => {
    try {
      return await loadWithLocation(api, (http) => getProduct(http, slug));
    } catch (error) {
      if (isApiError(error) && error.status === 404) return null;
      throw error;
    }
  },
);

const requestApiOnce = cache(requestApi);

export async function generateMetadata(props: PageProps<"/products/[slug]">): Promise<Metadata> {
  const { slug } = await props.params;
  const located = await loadProduct(await requestApiOnce(), slug).catch(() => null);
  if (!located) return { title: "Product not found", robots: { index: false } };
  const product = located.data;
  const description =
    product.description ??
    `${product.name}${product.localNames.length ? ` (${product.localNames.join(", ")})` : ""}, cleaned and cut to order.`;
  const image = product.images[0];
  return {
    title: product.name,
    description,
    alternates: { canonical: `/products/${product.slug}` },
    openGraph: {
      title: product.name,
      description,
      ...(image
        ? {
            images: [
              { url: image.url, width: image.width, height: image.height, alt: image.altText },
            ],
          }
        : {}),
    },
  };
}

export default async function ProductPage(props: PageProps<"/products/[slug]">) {
  const { slug } = await props.params;
  const api = await requestApiOnce();
  const located = await loadProduct(api, slug);
  if (!located) notFound();
  const product = located.data;

  const [serviceability, related] = await Promise.all([
    api.pincode && !located.pincodeRefused
      ? settle(getServiceability(api.anonymous, api.pincode))
      : null,
    settle(
      loadWithLocation(api, (http) =>
        listProducts(http, { category: [product.category.slug], pageSize: 5 }),
      ),
    ),
  ]);
  const relatedItems = related.ok
    ? related.value.data.items.filter((p) => p.id !== product.id).slice(0, 4)
    : [];
  const siteUrl = getSiteUrl();
  const canonical = siteUrl ? `${siteUrl}/products/${product.slug}` : null;
  const area = serviceability?.ok ? serviceability.value : null;

  return (
    <Container className="flex flex-col gap-10 py-8 pb-28 sm:pb-8">
      <script
        type="application/ld+json"
        // Built from server values only; "<" is escaped so the data cannot close the script element.
        dangerouslySetInnerHTML={{ __html: serializeJsonLd(productJsonLd(product, canonical)) }}
      />
      <Breadcrumbs
        items={[
          { href: "/", label: "Home" },
          { href: "/shop", label: "Shop" },
          { href: `/shop/${product.category.slug}`, label: product.category.name },
          { label: product.name },
        ]}
      />
      <LocationBanner pincode={api.pincode} pincodeRefused={located.pincodeRefused} />

      <div className="grid gap-8 lg:grid-cols-2 lg:gap-12">
        <Gallery images={product.images} name={product.name} />
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-2">
            <div className="flex flex-wrap gap-2">
              <Badge tone="accent">
                {sourceTypeLabels[product.sourceType] ?? product.sourceType}
              </Badge>
              {product.sourceRegion && <Badge>{product.sourceRegion}</Badge>}
            </div>
            <h1 className="text-text text-3xl font-bold sm:text-4xl">{product.name}</h1>
            {product.localNames.length > 0 && (
              <p className="text-text-muted">Also known as {product.localNames.join(", ")}</p>
            )}
            {product.speciesScientificName && (
              <p className="text-text-muted text-sm italic">{product.speciesScientificName}</p>
            )}
          </div>

          <PurchasePanel product={product} />

          <div className="flex flex-wrap items-center gap-3">
            <WishlistButton productId={product.id} slug={product.slug} />
          </div>

          {area?.serviceable && area.serviceArea && (
            <div className="border-border bg-surface flex gap-3 rounded-xl border p-4 text-sm">
              <Truck aria-hidden="true" className="text-accent-text mt-0.5 size-5 shrink-0" />
              <div className="flex flex-col gap-1">
                <p className="text-text font-semibold">Delivery to {area.pincode}</p>
                {area.nextSlot && (
                  <p className="text-text-muted">
                    Next available slot: {formatDayShort(area.nextSlot.startAt)},{" "}
                    {formatTime(area.nextSlot.startAt)} – {formatTime(area.nextSlot.endAt)}
                  </p>
                )}
                <p className="text-text-muted">
                  Delivery fee {formatRupees(area.serviceArea.deliveryFeePaise)}
                  {area.serviceArea.freeDeliveryAbovePaise != null &&
                    `, free above ${formatRupees(area.serviceArea.freeDeliveryAbovePaise)}`}
                  .
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      <Tabs defaultValue="description" className="flex flex-col gap-4">
        <TabsList>
          <TabsTrigger value="description">Description</TabsTrigger>
          <TabsTrigger value="storage">Storage and handling</TabsTrigger>
          <TabsTrigger value="sourcing">Sourcing</TabsTrigger>
        </TabsList>
        <TabsContent value="description" className="text-text max-w-3xl leading-7">
          {product.description ?? "Description coming soon."}
        </TabsContent>
        <TabsContent value="storage" className="text-text max-w-3xl leading-7">
          {product.storageInstructions ?? "Keep refrigerated and cook soon after delivery."}
        </TabsContent>
        <TabsContent value="sourcing" className="text-text max-w-3xl leading-7">
          <p>{sourceTypeLabels[product.sourceType] ?? product.sourceType}.</p>
          {product.sourceRegion && <p>Sourced from {product.sourceRegion}.</p>}
        </TabsContent>
      </Tabs>

      {relatedItems.length > 0 && (
        <Section
          title={`More ${product.category.name.toLowerCase()}`}
          action={{ href: `/shop/${product.category.slug}`, label: "See all" }}
        >
          <ProductGrid products={relatedItems} />
        </Section>
      )}
    </Container>
  );
}
