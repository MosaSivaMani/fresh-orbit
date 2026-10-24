"use client";

import { formatQuantity, formatRupees } from "@fresh/core";
import { IconButton, QuantityStepper, StatusBadge } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { getProduct } from "@/api/endpoints/catalog";
import { type CartLine } from "@/api/types/cart";
import { ProductImage } from "@/components/catalog/product-card";
import { useChangeCartItem, useRemoveCartItem } from "@/cart/use-cart";
import { describeError, fieldMessage } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { availabilityMap, sellUnitLabels } from "@/lib/status-maps";
import { useLocation } from "@/location/location-context";

/**
 * The variant's order bounds (min, max, step) for the line's stepper. The cart line does not carry them (W4,
 * docs/design/web.md §6), so they are read from the product rather than guessed; until they arrive the stepper is
 * disabled. The server still validates every change.
 */
function useLineBounds(line: CartLine) {
  const { pincode } = useLocation();
  const product = useQuery({
    queryKey: queryKeys.product(line.productSlug, line.preparation?.code, pincode),
    queryFn: () => getProduct(browserHttp(), line.productSlug, line.preparation?.code),
    enabled: line.isSellable,
    staleTime: 60_000,
  });
  const variant = product.data?.variants.find((v) => v.id === line.variantId);
  if (!variant) return null;
  return {
    min: variant.minOrder.value,
    max: variant.availability?.maxOrderableNow?.value ?? variant.maxOrder.value,
    step: variant.orderStep.value,
  };
}

/** One cart line: picture, name, the server's line total, a − n + stepper and remove. */
export function CartLineRow({ line }: { line: CartLine }) {
  const change = useChangeCartItem();
  const remove = useRemoveCartItem();
  const bounds = useLineBounds(line);
  const [error, setError] = useState<string | null>(null);
  const weighed = line.maxLineTotalPaise > line.estimatedLineTotalPaise;
  const formatValue = (v: number) => formatQuantity({ value: v, unit: line.quantity.unit });

  function setQuantity(value: number) {
    setError(null);
    if (value === 0) {
      remove.mutate(line.id, { onError: (e) => setError(describeError(e)) });
      return;
    }
    change.mutate(
      { itemId: line.id, body: { quantity: { value, unit: line.quantity.unit } } },
      {
        onError: (e) => setError(fieldMessage(e, "quantity.value", "quantity") ?? describeError(e)),
      },
    );
  }

  return (
    <li className="border-border flex gap-3 border-b border-dashed py-4 first:pt-0 last:border-b-0 last:pb-0 sm:gap-4">
      <Link
        href={`/products/${line.productSlug}`}
        className="bg-teal-soft w-20 shrink-0 self-start overflow-hidden rounded-2xl sm:w-24"
        tabIndex={-1}
        aria-hidden="true"
      >
        <ProductImage src={line.imageUrl} alt="" sizes="96px" className="aspect-square" />
      </Link>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 flex-col">
            <Link
              href={`/products/${line.productSlug}`}
              className="text-text leading-snug font-bold underline-offset-4 hover:underline"
            >
              {line.productName}
            </Link>
            <span className="text-text-muted text-[13px]">
              {line.variantName}
              {line.preparation && ` · ${line.preparation.name}`}
            </span>
          </div>
          <div className="shrink-0 text-right">
            <p className="font-display text-lg leading-tight font-extrabold tabular-nums">
              {formatRupees(line.estimatedLineTotalPaise)}
            </p>
            {weighed && (
              <p className="text-text-muted text-xs">
                at most {formatRupees(line.maxLineTotalPaise)}
              </p>
            )}
          </div>
        </div>

        <p className="text-text-muted flex flex-wrap items-center gap-x-2 gap-y-1 text-[12.5px]">
          {line.unitPricePaise !== null && (
            <span>
              {formatRupees(line.unitPricePaise)} {sellUnitLabels[line.priceUnit] ?? ""}
            </span>
          )}
          {weighed && <span>· packed up to {formatQuantity(line.maxChargeableQuantity)}</span>}
          {line.availability.status !== "IN_STOCK" && (
            <StatusBadge code={line.availability.status} map={availabilityMap} />
          )}
        </p>

        {line.issues.length > 0 && (
          <ul className="flex flex-col gap-1">
            {line.issues.map((issue) => (
              <li
                key={issue.code}
                className="bg-warning-subtle text-warning-text rounded-lg px-2 py-1 text-sm"
              >
                {issue.message}
              </li>
            ))}
          </ul>
        )}

        <div className="mt-1 flex items-center justify-between gap-2">
          {line.isSellable ? (
            <QuantityStepper
              size="sm"
              allowRemove
              label={line.productName}
              value={line.quantity.value}
              min={bounds?.min ?? line.quantity.value}
              max={bounds?.max ?? line.quantity.value}
              step={bounds?.step ?? 1}
              disabled={bounds === null}
              pending={change.isPending}
              formatValue={formatValue}
              onChange={setQuantity}
            />
          ) : (
            <span className="text-text-muted text-sm">{formatQuantity(line.quantity)}</span>
          )}
          <IconButton
            variant="ghost"
            size="icon-sm"
            label={`Remove ${line.productName}`}
            icon={<Trash2 aria-hidden="true" />}
            loading={remove.isPending}
            onClick={() => setQuantity(0)}
          />
        </div>
        {error && (
          <p role="alert" className="text-danger-text text-sm">
            {error}
          </p>
        )}
      </div>
    </li>
  );
}
