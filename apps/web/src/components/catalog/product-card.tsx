import { Badge, PriceTag, StatusBadge } from "@fresh/ui";
import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { type ProductListItem } from "@/api/types/catalog";
import { cx } from "@/lib/cx";
import { availabilityMap, sellUnitLabels, sourceTypeLabels } from "@/lib/status-maps";

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

/** Product tile for grids: everything shown comes from the list item; prices only with a location. */
export function ProductCard({
  product,
  priority = false,
}: {
  product: ProductListItem;
  priority?: boolean;
}) {
  const soldOut =
    product.availability?.status === "OUT_OF_STOCK" ||
    product.availability?.status === "NOT_SOLD_HERE";
  return (
    <article
      className={cx(
        "group border-border bg-surface relative flex flex-col overflow-hidden rounded-xl border shadow-xs transition-shadow duration-200 hover:shadow-md",
        soldOut && "opacity-75",
      )}
    >
      <ProductImage
        src={product.imageUrl}
        alt=""
        sizes="(min-width: 1024px) 25vw, (min-width: 640px) 33vw, 50vw"
        priority={priority}
      />
      <div className="flex flex-1 flex-col gap-2 p-4">
        <div className="flex flex-wrap gap-1.5">
          <Badge tone="accent">{sourceTypeLabels[product.sourceType] ?? product.sourceType}</Badge>
          {product.availability && product.availability.status !== "IN_STOCK" && (
            <StatusBadge code={product.availability.status} map={availabilityMap} dot={false} />
          )}
        </div>
        <h3 className="text-text text-base leading-6 font-semibold">
          <Link
            href={`/products/${product.slug}`}
            className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none"
          >
            {product.name}
          </Link>
        </h3>
        {product.localNames.length > 0 && (
          <p className="text-text-muted line-clamp-1 text-sm">{product.localNames.join(" · ")}</p>
        )}
        <div className="mt-auto pt-1">
          {product.fromPrice ? (
            <PriceTag
              paise={product.fromPrice.unitPricePaise}
              mrpPaise={product.fromPrice.compareAtPaise}
              per={sellUnitLabels[product.fromPrice.perUnit]}
              size="sm"
            />
          ) : (
            <p className="text-text-muted text-sm">Set your pincode to see prices</p>
          )}
          {product.availability?.message && (
            <p className="text-text-muted mt-1 text-xs">{product.availability.message}</p>
          )}
        </div>
      </div>
    </article>
  );
}

export function ProductGrid({
  products,
  priorityCount = 0,
}: {
  products: ProductListItem[];
  priorityCount?: number;
}) {
  return (
    <ul className="grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-4">
      {products.map((product, index) => (
        <li key={product.id} className="flex">
          <div className="w-full">
            <ProductCard product={product} priority={index < priorityCount} />
          </div>
        </li>
      ))}
    </ul>
  );
}

export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <ul className="grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-4" aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <li key={i} className="border-border bg-surface overflow-hidden rounded-xl border">
          <div className="bg-muted aspect-[4/3] animate-pulse" />
          <div className="flex flex-col gap-2 p-4">
            <div className="bg-muted h-4 w-2/3 animate-pulse rounded" />
            <div className="bg-muted h-3 w-1/2 animate-pulse rounded" />
            <div className="bg-muted h-5 w-1/3 animate-pulse rounded" />
          </div>
        </li>
      ))}
    </ul>
  );
}
