import { Fish } from "lucide-react";
import Image from "next/image";
import { type HomeBanner, type ProductListItem } from "@/api/types/catalog";
import { cx } from "@/lib/cx";
import { ShopProductCard } from "./shop-product-card";

export function ProductImage({
  src,
  alt,
  sizes,
  priority = false,
  className,
}: {
  src: string | null;
  alt: string;
  sizes: string;
  priority?: boolean;
  className?: string;
}) {
  return (
    <div className={cx("bg-muted relative aspect-[4/3] overflow-hidden", className)}>
      {src ? (
        <Image
          src={src}
          alt={alt}
          fill
          sizes={sizes}
          priority={priority}
          className="object-cover"
        />
      ) : (
        <div className="text-text-muted flex h-full items-center justify-center" aria-hidden="true">
          <Fish className="size-12 opacity-50" />
        </div>
      )}
    </div>
  );
}

/**
 * Offer lines per product slug, from the server's promotions: a banner that links to a product puts its title on
 * that product's picture. Nothing is invented for products without one.
 */
export function offersBySlug(banners: readonly HomeBanner[]): Record<string, string> {
  const offers: Record<string, string> = {};
  for (const banner of banners) {
    if (banner.linkType === "PRODUCT" && banner.linkTarget && !offers[banner.linkTarget]) {
      offers[banner.linkTarget] = banner.title;
    }
  }
  return offers;
}

const gridClass =
  "grid grid-cols-1 gap-x-4 gap-y-6 min-[480px]:grid-cols-2 md:grid-cols-3 md:gap-x-[22px] lg:grid-cols-4";

export function ProductGrid({
  products,
  priorityCount = 0,
  offers,
  freshness,
}: {
  products: ProductListItem[];
  priorityCount?: number;
  /** From `offersBySlug`. */
  offers?: Record<string, string>;
  /** The server's freshness or slot line for cards without their own message. */
  freshness?: string;
}) {
  return (
    <ul className={gridClass}>
      {products.map((product, index) => (
        <li key={product.id} className="min-w-0">
          <ShopProductCard
            product={product}
            index={index}
            priority={index < priorityCount}
            offer={offers?.[product.slug]}
            freshness={freshness}
          />
        </li>
      ))}
    </ul>
  );
}

export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <ul className={gridClass} aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <li key={i} className="flex flex-col gap-2.5">
          <div className="bg-muted aspect-[16/11] animate-pulse rounded-[20px]" />
          <div className="flex flex-col gap-2 px-1.5">
            <div className="bg-muted h-4 w-2/3 animate-pulse rounded" />
            <div className="bg-muted h-3 w-1/2 animate-pulse rounded" />
            <div className="bg-muted h-5 w-1/3 animate-pulse rounded" />
          </div>
        </li>
      ))}
    </ul>
  );
}
