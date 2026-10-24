"use client";

import { formatPercentFromBp, formatQuantity, formatRupees } from "@fresh/core";
import { Button, PriceTag, QuantityStepper, RadioGroup, StatusBadge, toast } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBag } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { getProduct } from "@/api/endpoints/catalog";
import { type Preparation, type ProductDetail, type Variant } from "@/api/types/catalog";
import { useAddToCart, useCart } from "@/cart/use-cart";
import { describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { availabilityMap, sellUnitLabels } from "@/lib/status-maps";
import { useLocation } from "@/location/location-context";

function chargeText(p: Preparation): string {
  if (p.chargePaise === null) return "Not offered at your store";
  if (p.chargePaise === 0) return "No preparation charge";
  const amount = formatRupees(p.chargePaise);
  switch (p.chargeBasis) {
    case "PER_KG":
      return `${amount} per kg preparation charge`;
    case "PER_ITEM":
      return `${amount} per piece preparation charge`;
    default:
      return `${amount} preparation charge`;
  }
}

function defaultPreparation(variant: Variant | undefined): string | undefined {
  return (variant?.preparations.find((p) => p.isDefault) ?? variant?.preparations[0])?.code;
}

export interface PurchasePanelProps {
  /** Server-rendered product for the default preparation (initial data). */
  product: ProductDetail;
}

/**
 * Variant, preparation and quantity choice, then "Add to cart". Bounds and steps are the server's; the estimate for the
 * chosen weight is the server's cart line (docs/api §5), shown once the item is in the cart.
 */
export function PurchasePanel({ product: initial }: PurchasePanelProps) {
  const { pincode, setPickerOpen } = useLocation();
  const [variantId, setVariantId] = useState(initial.variants[0]?.id ?? "");
  const initialVariant = initial.variants.find((v) => v.id === variantId);
  const [preparation, setPreparation] = useState(defaultPreparation(initialVariant));
  const [quantity, setQuantity] = useState(initialVariant?.minOrder.value ?? 0);
  const [error, setError] = useState<string | null>(null);

  const isDefaultPrep = preparation === defaultPreparation(initialVariant);
  // `maxOrderableNow` depends on the preparation (yield); ask the server for the chosen one.
  const detail = useQuery({
    queryKey: queryKeys.product(initial.slug, preparation, pincode),
    queryFn: () => getProduct(browserHttp(), initial.slug, preparation),
    enabled: pincode !== null && !isDefaultPrep,
    initialData: isDefaultPrep ? initial : undefined,
    placeholderData: (previous) => previous,
  });
  const product = detail.data ?? initial;
  const variant = product.variants.find((v) => v.id === variantId) ?? product.variants[0];
  const cart = useCart();
  const add = useAddToCart();

  if (!variant) return null;

  const prep = variant.preparations.find((p) => p.code === preparation);
  const availability = variant.availability;
  const priced = variant.unitPricePaise !== null;
  const unavailable =
    availability?.status === "OUT_OF_STOCK" || availability?.status === "NOT_SOLD_HERE";
  const maxNow = availability?.maxOrderableNow?.value ?? variant.maxOrder.value;
  const inCart = cart.data?.lines.find(
    (line) => line.variantId === variant.id && line.preparation?.code === preparation,
  );

  function selectVariant(id: string) {
    const next = product.variants.find((v) => v.id === id);
    setVariantId(id);
    setPreparation(defaultPreparation(next));
    if (next) setQuantity(next.minOrder.value);
    setError(null);
  }

  async function addToCart() {
    setError(null);
    if (!pincode) {
      setPickerOpen(true);
      return;
    }
    try {
      await add.mutateAsync({
        body: {
          variantId: variant!.id,
          ...(preparation ? { preparationCode: preparation } : {}),
          quantity: { value: quantity, unit: variant!.minOrder.unit },
        },
        pincode,
      });
      toast({ title: `${product.name} added to your cart`, tone: "success" });
    } catch (e) {
      if (hasCode(e, "LOCATION_REQUIRED", "PINCODE_NOT_SERVICEABLE")) {
        setPickerOpen(true);
        return;
      }
      setError(
        fieldMessage(e, "quantity.value", "quantity", "preparationCode") ?? describeError(e),
      );
    }
  }

  const formatValue = (v: number) => formatQuantity({ value: v, unit: variant.minOrder.unit });

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        {priced ? (
          <PriceTag
            paise={variant.unitPricePaise!}
            mrpPaise={variant.compareAtPaise}
            per={sellUnitLabels[variant.sellUnit]}
            size="lg"
          />
        ) : (
          <p className="text-text-muted">
            {pincode ? "Not sold at your store right now." : "Set your pincode to see the price."}
          </p>
        )}
        {availability && (
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge code={availability.status} map={availabilityMap} />
            {availability.message && (
              <span className="text-text-muted text-sm">{availability.message}</span>
            )}
          </div>
        )}
      </div>

      {product.variants.length > 1 && (
        <RadioGroup
          label="Size"
          value={variant.id}
          onValueChange={selectVariant}
          options={product.variants.map((v) => ({ value: v.id, label: v.name }))}
        />
      )}
      {product.variants.length === 1 && (
        <p className="text-text text-sm">
          <span className="font-medium">Size:</span> {variant.name}
        </p>
      )}

      {variant.preparations.length > 0 && (
        <RadioGroup
          label="Preparation"
          value={preparation}
          onValueChange={(code) => {
            setPreparation(code);
            setError(null);
          }}
          options={variant.preparations.map((p) => ({
            value: p.code,
            label: p.name,
            description: `${chargeText(p)}${p.yieldBp < 10000 ? ` · about ${formatPercentFromBp(p.yieldBp)} of the whole weight remains after cleaning` : ""}`,
            ...(p.chargePaise === null ? { disabled: true } : {}),
          }))}
        />
      )}

      <div className="flex flex-col gap-2">
        <span className="text-text text-sm font-medium" id="qty-label">
          {variant.isWeightVariable ? "Net weight after cleaning" : "Quantity"}
        </span>
        <QuantityStepper
          label={product.name}
          value={quantity}
          min={variant.minOrder.value}
          max={maxNow}
          step={variant.orderStep.value}
          formatValue={formatValue}
          onChange={setQuantity}
          disabled={!priced || unavailable}
        />
        <p className="text-text-muted text-xs">
          From {formatValue(variant.minOrder.value)} to {formatValue(variant.maxOrder.value)}, in
          steps of {formatValue(variant.orderStep.value)}.
        </p>
      </div>

      {variant.isWeightVariable && (
        <p className="bg-accent-subtle text-accent-text rounded-lg p-3 text-sm">
          {product.netWeightNote}
        </p>
      )}

      {inCart ? (
        <div className="border-border bg-surface rounded-lg border p-4 text-sm" aria-live="polite">
          <p className="text-text font-semibold">
            In your cart: {formatQuantity(inCart.quantity)}, {inCart.preparation?.name}
          </p>
          <p className="text-text-muted">
            Estimated {formatRupees(inCart.estimatedLineTotalPaise)}
            {inCart.maxLineTotalPaise !== inCart.estimatedLineTotalPaise &&
              `; at most ${formatRupees(inCart.maxLineTotalPaise)} if packed up to ${formatQuantity(inCart.maxChargeableQuantity)}`}
            .
          </p>
          <Link
            href="/cart"
            className="text-accent-text font-semibold underline-offset-4 hover:underline"
          >
            Go to cart
          </Link>
        </div>
      ) : (
        priced &&
        !unavailable && (
          <p className="text-text-muted text-xs">
            The estimated and maximum price for your weight are shown as soon as it is in your cart.
          </p>
        )
      )}

      {error && (
        <p role="alert" className="text-danger-text text-sm">
          {error}
        </p>
      )}

      <div className="border-border bg-surface/95 fixed inset-x-0 bottom-0 z-[1100] flex items-center gap-3 border-t p-3 backdrop-blur sm:static sm:border-0 sm:bg-transparent sm:p-0">
        {priced && (
          <div className="sm:hidden">
            <PriceTag
              paise={variant.unitPricePaise!}
              per={sellUnitLabels[variant.sellUnit]}
              size="sm"
            />
          </div>
        )}
        <Button
          size="lg"
          className="flex-1 sm:flex-none"
          leadingIcon={<ShoppingBag aria-hidden="true" />}
          loading={add.isPending}
          disabled={(pincode !== null && !priced) || unavailable}
          onClick={() => void addToCart()}
        >
          {pincode ? (unavailable ? "Not available" : "Add to cart") : "Set pincode to buy"}
        </Button>
      </div>
      {prep && (
        <span className="sr-only" aria-live="polite">
          {prep.name} selected
        </span>
      )}
    </div>
  );
}
