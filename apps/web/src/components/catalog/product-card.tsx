import { Fish } from "lucide-react";
import Image from "next/image";
import { type ProductListItem } from "@/api/types/catalog";
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

export function ProductGrid({
  products,
  priorityCount = 0,
}: {
  products: ProductListItem[];
  priorityCount?: number;
}) {
  return (
    <ul className="grid grid-cols-2 gap-x-3 gap-y-5 sm:grid-cols-3 sm:gap-x-4 lg:grid-cols-4">
      {products.map((product, index) => (
        <li key={product.id} className="flex">
          <div className="w-full">
            <ShopProductCard product={product} index={index} priority={index < priorityCount} />
          </div>
        </li>
      ))}
    </ul>
  );
}

export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <ul
      className="grid grid-cols-2 gap-x-3 gap-y-5 sm:grid-cols-3 sm:gap-x-4 lg:grid-cols-4"
      aria-hidden="true"
    >
      {Array.from({ length: count }, (_, i) => (
        <li key={i} className="bg-surface-paper shadow-soft-md overflow-hidden rounded-[20px]">
          <div className="bg-muted h-32 animate-pulse sm:h-40" />
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
