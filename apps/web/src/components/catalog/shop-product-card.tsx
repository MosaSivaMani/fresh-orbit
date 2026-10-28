"use client";

import { formatQuantity } from "@fresh/core";
import { FoodCard, type FoodCardTone, PriceTag, toast } from "@fresh/ui";
import { useQueryClient } from "@tanstack/react-query";
import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { getProduct } from "@/api/endpoints/catalog";
import { type AddCartItemRequest } from "@/api/types/cart";
import { type ProductDetail, type ProductListItem } from "@/api/types/catalog";
import { lineBounds } from "@/cart/line-bounds";
import { useAddToCart, useCart, useChangeCartItem, useRemoveCartItem } from "@/cart/use-cart";
import { describeError, hasCode } from "@/lib/errors";
import { discountLabel, freshnessLine, servingLine } from "@/lib/product-facts";
import { queryKeys } from "@/lib/query-keys";
import { availabilityMap, sellUnitLabels } from "@/lib/status-maps";
import { useLocation } from "@/location/location-context";

const tones: FoodCardTone[] = ["ice", "teal", "coral", "mango"];

/**
 * What ADD on a card puts in the cart, or null to open the product page instead. With the server's default variant
 * (CAT-R23, gap W3 closed: the size behind the card's "from" price) it is that size, its default cut and its minimum
 * quantity, with no extra request. A list item without one (an older API) falls back to reading the product: one
 * size is added the same way, several open the product page. A product with no price here is never added.
 */
export async function addRequestFor(
  product: ProductListItem,
  readProduct: () => Promise<ProductDetail>,
): Promise<AddCartItemRequest | null> {
  if (!product.fromPrice) return null;
  const preferred = product.defaultVariant;
  if (preferred) {
    return {
      variantId: preferred.id,
      ...(preferred.defaultPreparationCode
        ? { preparationCode: preferred.defaultPreparationCode }
        : {}),
      quantity: { value: preferred.minOrder.value, unit: preferred.minOrder.unit },
    };
  }
  const full = await readProduct();
  const only = full.variants.length === 1 ? full.variants[0] : undefined;
  if (!only || only.unitPricePaise === null) return null;
  const prep = only.preparations.find((p) => p.isDefault) ?? only.preparations[0];
  return {
    variantId: only.id,
    ...(prep ? { preparationCode: prep.code } : {}),
    quantity: { value: only.minOrder.value, unit: only.minOrder.unit },
  };
}

export interface ShopProductCardProps {
  product: ProductListItem;
  index?: number;
  priority?: boolean;
  /**
   * Fallback offer line (a promotion banner's title) when the server sends no `offer` for the item (CAT-R26).
   */
  offer?: string;
  /** Fallback freshness line from the server (e.g. the next delivery slot) when the item has no message. */
  freshness?: string;
}

/**
 * The website's picture card (D95) for a list item. ADD puts the server's default size in the cart (see
 * `addRequestFor`, D255). Once the cart holds exactly one line of this product, ADD becomes a − n + stepper for that
 * line with the line's own bounds (W4). Bounds, prices and the cart are always the server's (rule 4).
 */
export function ShopProductCard({
  product,
  index = 0,
  priority = false,
  offer,
  freshness,
}: ShopProductCardProps) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const cart = useCart();
  const { pincode, setPickerOpen } = useLocation();
  const add = useAddToCart();
  const change = useChangeCartItem();
  const remove = useRemoveCartItem();
  const [adding, setAdding] = useState(false);
  const lines = cart.data?.lines.filter((l) => l.productId === product.id) ?? [];
  const line = lines.length === 1 ? lines[0] : undefined;
  // The stepper's bounds come on the cart line itself (W4); none means no stepper.
  const bounds = line ? lineBounds(line) : null;

  const status = product.availability?.status;
  const soldOut = status === "OUT_OF_STOCK" || status === "NOT_SOLD_HERE";
  const href = `/products/${product.slug}`;
  const onError = (e: unknown) => toast({ title: describeError(e), tone: "danger" });
  // The default variant's serving facts (CAT-R21, CAT-R23), else the local names.
  const localNames = product.localNames.length > 0 ? product.localNames.join(" · ") : undefined;
  const meta = servingLine(product.defaultVariant?.serving) ?? localNames;
  const fresh = product.availability?.message ?? freshnessLine(product.freshness) ?? freshness;

  async function onAdd() {
    if (!pincode) {
      setPickerOpen(true);
      return;
    }
    setAdding(true);
    try {
      const body = await addRequestFor(product, () =>
        queryClient.fetchQuery({
          queryKey: queryKeys.product(product.slug, undefined, pincode),
          queryFn: () => getProduct(browserHttp(), product.slug),
          staleTime: 60_000,
        }),
      );
      if (!body) {
        router.push(href);
        return;
      }
      await add.mutateAsync({ body, pincode });
      toast({ title: `${product.name} added to your cart`, tone: "success" });
    } catch (e) {
      if (hasCode(e, "LOCATION_REQUIRED", "PINCODE_NOT_SERVICEABLE")) setPickerOpen(true);
      else onError(e);
    } finally {
      setAdding(false);
    }
  }

  function onQuantityChange(value: number) {
    if (!line) return;
    if (value === 0) remove.mutate(line.id, { onError });
    else
      change.mutate(
        { itemId: line.id, body: { quantity: { value, unit: line.quantity.unit } } },
        { onError },
      );
  }

  return (
    <FoodCard
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
            sizes="(min-width: 1024px) 25vw, (min-width: 768px) 33vw, (min-width: 480px) 50vw, 100vw"
            priority={priority}
            className="object-cover"
          />
        ) : (
          <Fish className="text-accent-text size-16" strokeWidth={1.5} />
        )
      }
      offer={product.offer?.text ?? offer}
      freshness={fresh}
      meta={meta}
      price={
        product.fromPrice ? (
          <PriceTag
            paise={product.fromPrice.unitPricePaise}
            mrpPaise={product.fromPrice.compareAtPaise}
            per={sellUnitLabels[product.fromPrice.perUnit]}
            savingsLabel={discountLabel(product.fromPrice.discountBp)}
            size="sm"
          />
        ) : (
          <span className="text-text-muted font-sans text-xs">Set your pincode to see prices</span>
        )
      }
      unavailableLabel={soldOut && status ? availabilityMap[status]?.label : undefined}
      {...(lines.length > 1 ? { addHref: href } : { onAdd: () => void onAdd() })}
      quantity={line && bounds ? line.quantity.value : 0}
      min={bounds?.min}
      max={bounds?.max}
      step={bounds?.step}
      formatQuantity={
        line ? (v) => formatQuantity({ value: v, unit: line.quantity.unit }) : undefined
      }
      onQuantityChange={onQuantityChange}
      pending={adding || add.isPending || change.isPending || remove.isPending}
    />
  );
}
