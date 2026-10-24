"use client";

import { formatQuantity } from "@fresh/core";
import { type BubbleTone, PriceTag, ProductCard, toast } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { getProduct } from "@/api/endpoints/catalog";
import { type ProductListItem } from "@/api/types/catalog";
import { useCart, useChangeCartItem, useRemoveCartItem } from "@/cart/use-cart";
import { describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { availabilityMap, sellUnitLabels, sourceTypeLabels } from "@/lib/status-maps";
import { useLocation } from "@/location/location-context";

const tones: BubbleTone[] = ["teal", "coral", "mango", "ice"];

/**
 * Shop card (D88): ADD sits on the picture's edge. A list item has no variant id, so ADD opens the product page to
 * choose size and cut. Once the cart holds exactly one line of this product, ADD becomes a − n + stepper for that
 * line; its bounds come from the product endpoint and every change is answered by the server's new cart.
 */
export function ShopProductCard({
  product,
  index = 0,
  priority = false,
}: {
  product: ProductListItem;
  index?: number;
  priority?: boolean;
}) {
  const cart = useCart();
  const { pincode } = useLocation();
  const change = useChangeCartItem();
  const remove = useRemoveCartItem();
  const lines = cart.data?.lines.filter((l) => l.productId === product.id) ?? [];
  const line = lines.length === 1 ? lines[0] : undefined;
  const detail = useQuery({
    queryKey: queryKeys.product(product.slug, line?.preparation?.code, pincode),
    queryFn: () => getProduct(browserHttp(), product.slug, line?.preparation?.code),
    enabled: line !== undefined,
    staleTime: 60_000,
  });
  const variant = line ? detail.data?.variants.find((v) => v.id === line.variantId) : undefined;

  const status = product.availability?.status;
  const soldOut = status === "OUT_OF_STOCK" || status === "NOT_SOLD_HERE";
  const href = `/products/${product.slug}`;

  function onQuantityChange(value: number) {
    if (!line) return;
    const onError = (e: unknown) => toast({ title: describeError(e), tone: "danger" });
    if (value === 0) remove.mutate(line.id, { onError });
    else
      change.mutate(
        { itemId: line.id, body: { quantity: { value, unit: line.quantity.unit } } },
        { onError },
      );
  }

  return (
    <ProductCard
      name={product.name}
      label={product.name}
      href={href}
      renderLink={({ href: to, className, children }) => (
        <Link href={to} className={className}>
          {children}
        </Link>
      )}
      pictureTone={tones[index % tones.length]}
      picture={
        product.imageUrl ? (
          <Image
            src={product.imageUrl}
            alt=""
            fill
            sizes="(min-width: 1024px) 25vw, (min-width: 640px) 33vw, 50vw"
            priority={priority}
            className="object-cover"
          />
        ) : (
          <Fish className="text-accent-text size-14" strokeWidth={1.5} />
        )
      }
      badge={sourceTypeLabels[product.sourceType] ?? product.sourceType}
      detail={
        <>
          {product.localNames.length > 0 && (
            <span className="line-clamp-1">{product.localNames.join(" · ")}</span>
          )}
          {product.availability?.message && (
            <span className="block">{product.availability.message}</span>
          )}
        </>
      }
      price={
        product.fromPrice ? (
          <PriceTag
            paise={product.fromPrice.unitPricePaise}
            mrpPaise={product.fromPrice.compareAtPaise}
            per={sellUnitLabels[product.fromPrice.perUnit]}
            size="sm"
          />
        ) : (
          <span className="text-text-muted font-sans text-xs">Set your pincode to see prices</span>
        )
      }
      unavailableLabel={soldOut && status ? availabilityMap[status]?.label : undefined}
      addHref={href}
      quantity={line && variant ? line.quantity.value : 0}
      min={variant?.minOrder.value}
      max={variant?.availability?.maxOrderableNow?.value ?? variant?.maxOrder.value}
      step={variant?.orderStep.value}
      formatQuantity={line ? (v) => formatQuantity({ value: v, unit: line.quantity.unit }) : undefined}
      onQuantityChange={onQuantityChange}
      pending={change.isPending || remove.isPending}
    />
  );
}
