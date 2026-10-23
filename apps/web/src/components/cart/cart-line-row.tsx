"use client";

import { formatQuantity, formatRupees } from "@fresh/core";
import { Button, Dialog, DialogContent, QuantityStepper, Skeleton, StatusBadge } from "@fresh/ui";
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
 * The cart line does not carry the variant's order bounds (min, max, step), so the quantity editor asks the product
 * endpoint for them when opened rather than guessing (docs/design/web.md, API gaps).
 */
function QuantityEditor({ line, onClose }: { line: CartLine; onClose: () => void }) {
  const { pincode } = useLocation();
  const change = useChangeCartItem();
  const [value, setValue] = useState(line.quantity.value);
  const [error, setError] = useState<string | null>(null);
  const product = useQuery({
    queryKey: queryKeys.product(line.productSlug, line.preparation.code, pincode),
    queryFn: () => getProduct(browserHttp(), line.productSlug, line.preparation.code),
  });
  const variant = product.data?.variants.find((v) => v.id === line.variantId);
  const formatValue = (v: number) => formatQuantity({ value: v, unit: line.quantity.unit });

  async function save() {
    setError(null);
    try {
      await change.mutateAsync({
        itemId: line.id,
        body: { quantity: { value, unit: line.quantity.unit } },
      });
      onClose();
    } catch (e) {
      setError(fieldMessage(e, "quantity.value", "quantity") ?? describeError(e));
    }
  }

  return (
    <DialogContent
      title={`Change ${line.productName}`}
      description={`${line.variantName}, ${line.preparation.name}`}
      size="sm"
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={() => void save()}
            loading={change.isPending}
            disabled={!variant || value === line.quantity.value}
          >
            Update
          </Button>
        </>
      }
    >
      {product.isPending ? (
        <Skeleton className="h-11 w-48" />
      ) : variant ? (
        <div className="flex flex-col gap-2">
          <QuantityStepper
            label={line.productName}
            value={value}
            min={variant.minOrder.value}
            max={variant.availability?.maxOrderableNow?.value ?? variant.maxOrder.value}
            step={variant.orderStep.value}
            formatValue={formatValue}
            onChange={setValue}
          />
          <p className="text-text-muted text-xs">
            From {formatValue(variant.minOrder.value)} to {formatValue(variant.maxOrder.value)}, in
            steps of {formatValue(variant.orderStep.value)}. The new estimate appears in your cart
            once updated.
          </p>
        </div>
      ) : (
        <p className="text-text-muted text-sm">
          This item can no longer be changed. Remove it and add it again from the product page.
        </p>
      )}
      {error && (
        <p role="alert" className="text-danger-text text-sm">
          {error}
        </p>
      )}
    </DialogContent>
  );
}

export function CartLineRow({ line }: { line: CartLine }) {
  const remove = useRemoveCartItem();
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const weighed = line.maxLineTotalPaise > line.estimatedLineTotalPaise;

  return (
    <li className="border-border flex gap-4 border-b py-5 last:border-b-0">
      <Link
        href={`/products/${line.productSlug}`}
        className="w-24 shrink-0 overflow-hidden rounded-lg sm:w-28"
        tabIndex={-1}
        aria-hidden="true"
      >
        <ProductImage src={line.imageUrl} alt="" sizes="112px" />
      </Link>
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="flex min-w-0 flex-col">
            <Link
              href={`/products/${line.productSlug}`}
              className="text-text font-semibold underline-offset-4 hover:underline"
            >
              {line.productName}
            </Link>
            <span className="text-text-muted text-sm">
              {line.variantName} · {line.preparation.name}
            </span>
          </div>
          <div className="text-right">
            <p className="text-text font-semibold tabular-nums">
              {formatRupees(line.estimatedLineTotalPaise)}
            </p>
            {weighed && (
              <p className="text-text-muted text-xs">
                at most {formatRupees(line.maxLineTotalPaise)}
              </p>
            )}
          </div>
        </div>

        <div className="text-text-muted flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          <span>
            {formatQuantity(line.quantity)}
            {line.unitPricePaise !== null &&
              ` · ${formatRupees(line.unitPricePaise)} ${sellUnitLabels[line.priceUnit] ?? ""}`}
          </span>
          {weighed && <span>packed up to {formatQuantity(line.maxChargeableQuantity)}</span>}
          {line.availability.status !== "IN_STOCK" && (
            <StatusBadge code={line.availability.status} map={availabilityMap} />
          )}
        </div>

        {line.issues.length > 0 && (
          <ul className="flex flex-col gap-1">
            {line.issues.map((issue) => (
              <li
                key={issue.code}
                className="bg-warning-subtle text-warning-text rounded-md px-2 py-1 text-sm"
              >
                {issue.message}
              </li>
            ))}
          </ul>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <Dialog open={editing} onOpenChange={setEditing}>
            {line.isSellable && (
              <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                Change quantity
              </Button>
            )}
            {editing && <QuantityEditor line={line} onClose={() => setEditing(false)} />}
          </Dialog>
          <Button
            variant="ghost"
            size="sm"
            loading={remove.isPending}
            leadingIcon={<Trash2 aria-hidden="true" />}
            aria-label={`Remove ${line.productName}`}
            onClick={() => {
              setError(null);
              remove.mutate(line.id, { onError: (e) => setError(describeError(e)) });
            }}
          >
            Remove
          </Button>
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
