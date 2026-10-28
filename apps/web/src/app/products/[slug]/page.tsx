import { formatDayShort, formatRupees, formatTime, isApiError } from "@fresh/core";
import {
  Heading,
  Script,
  Sparks,
  Sticker,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@fresh/ui";
import { Clock, Fish, MapPin, Truck } from "lucide-react";
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
import { GetTheApp } from "@/components/get-the-app";
import { Container, Section } from "@/components/section";
import { productJsonLd, serializeJsonLd } from "@/lib/json-ld";
import { settle } from "@/lib/settle";
import { slotText } from "@/lib/slot-text";
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
  // Slot times in the store's own zone (W10).
  const zone = area?.city?.timeZone;

  return (
    <Container className="flex flex-col gap-10 py-6 pb-32 sm:py-8 md:pb-8">
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

      <div className="grid gap-6 md:grid-cols-2 md:gap-10">
        {/* The tinted picture panel (D95), as on the cards. */}
        <div className="bg-ice-soft relative self-start rounded-[28px] p-3 sm:p-4">
          <Gallery images={product.images} name={product.name} />
          <Sticker tone="mango" size="sm" className="absolute top-5 left-5 z-[1]">
            {sourceTypeLabels[product.sourceType] ?? product.sourceType}
          </Sticker>
        </div>
        <div className="flex flex-col gap-5">
          <div className="flex flex-col gap-1.5">
            <Heading level={1} size="page" className="text-text sm:text-[2.5rem]">
              {product.name}
              {product.localNames[0] && (
                <>
                  {" "}
                  <Script tone="accent">{product.localNames[0]}</Script>
                </>
              )}
              <Sparks className="text-accent-coral ml-1 w-7 -translate-y-4" />
            </Heading>
            {product.localNames.length > 1 && (
              <p className="text-text-muted">
                Also known as {product.localNames.slice(1).join(", ")}
              </p>
            )}
            {product.speciesScientificName && (
              <p className="text-text-muted text-sm italic">{product.speciesScientificName}</p>
            )}
          </div>

          <ul className="grid grid-cols-3 gap-2" aria-label="About this product">
            <li className="bg-surface-paper shadow-soft-sm grid justify-items-center gap-1.5 rounded-[20px] px-2 py-3 text-center text-xs font-bold">
              <span className="bg-teal-soft text-accent-text grid size-9 place-items-center rounded-full">
                <Fish aria-hidden="true" className="size-[18px]" />
              </span>
              {sourceTypeLabels[product.sourceType] ?? product.sourceType}
            </li>
            <li className="bg-surface-paper shadow-soft-sm grid justify-items-center gap-1.5 rounded-[20px] px-2 py-3 text-center text-xs font-bold">
              <span className="bg-teal-soft text-accent-text grid size-9 place-items-center rounded-full">
                <MapPin aria-hidden="true" className="size-[18px]" />
              </span>
              {product.sourceRegion ?? product.category.name}
            </li>
            <li className="bg-surface-paper shadow-soft-sm grid justify-items-center gap-1.5 rounded-[20px] px-2 py-3 text-center text-xs font-bold">
              <span className="bg-teal-soft text-accent-text grid size-9 place-items-center rounded-full">
                <Clock aria-hidden="true" className="size-[18px]" />
              </span>
              {area?.nextSlot
                ? `Next slot ${formatDayShort(area.nextSlot.startAt, zone)}, ${formatTime(area.nextSlot.startAt, zone)}`
                : "Pick a slot at checkout"}
            </li>
          </ul>

          <PurchasePanel product={product} />

          <div className="flex flex-wrap items-center gap-3">
            <WishlistButton productId={product.id} slug={product.slug} />
          </div>

          {area?.serviceable && area.serviceArea && (
            <div className="bg-surface-sand flex gap-3 rounded-[20px] p-4 text-sm">
              <Truck aria-hidden="true" className="text-accent-text mt-0.5 size-5 shrink-0" />
              <div className="flex flex-col gap-1">
                <p className="text-text font-semibold">
                  Delivery to {area.pincode}
                  {area.city ? `, ${area.city.name}` : ""}
                </p>
                {area.nextSlot && (
                  <p className="text-text-muted">
                    Next available slot: {slotText(area.nextSlot, zone)}
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
          title="More"
          script={product.category.name.toLowerCase()}
          action={{ href: `/shop/${product.category.slug}`, label: "See all" }}
        >
          <ProductGrid
            products={relatedItems}
            {...(area?.nextSlot
              ? {
                  freshness: `Next slot ${slotText(area.nextSlot, zone)}`,
                }
              : {})}
          />
        </Section>
      )}
      <GetTheApp />
    </Container>
  );
}
