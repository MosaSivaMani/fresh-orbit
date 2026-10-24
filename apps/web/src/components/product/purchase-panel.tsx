"use client";

import { formatPercentFromBp, formatQuantity, formatRupees } from "@fresh/core";
import { Button, OptionTile, PriceTag, QuantityStepper, StatusBadge, toast } from "@fresh/ui";
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
 * Size and cut chosen on tiles (D95), quantity on the stepper, then the teal "Add to cart" bar. Bounds and steps are the server's; the estimate for the
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
    <div className="flex flex-col gap-5">
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
        <fieldset className="flex flex-col gap-2">
          <legend className="text-text font-display mb-2 text-base font-extrabold">
            Choose your size
          </legend>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {product.variants.map((v) => (
              <OptionTile
                key={v.id}
                name="variant"
                value={v.id}
                label={v.name}
                checked={v.id === variant.id}
                onChange={() => selectVariant(v.id)}
              />
            ))}
          </div>
        </fieldset>
      )}
      {product.variants.length === 1 && (
        <p className="text-text text-sm">
          <span className="font-semibold">Size:</span> {variant.name}
        </p>
      )}
      {(variant.packSizeGrams !== null || variant.avgPieceGrams !== null) && (
        <p className="text-text-muted text-sm">
          {[
            variant.packSizeGrams !== null &&
              `Pack of ${formatQuantity({ value: variant.packSizeGrams, unit: "GRAM" })}`,
            variant.avgPieceGrams !== null &&
              `about ${formatQuantity({ value: variant.avgPieceGrams, unit: "GRAM" })} a piece`,
          ]
            .filter(Boolean)
            .join(" · ")}
        </p>
      )}

      {variant.preparations.length > 0 && (
        <fieldset className="flex flex-col gap-2">
          <legend className="text-text font-display mb-2 text-base font-extrabold">
            Choose your cut
          </legend>
          <div className="grid grid-cols-2 gap-2">
            {variant.preparations.map((p) => (
              <OptionTile
                key={p.code}
                name="preparation"
                value={p.code}
                label={p.name}
                description={`${chargeText(p)}${p.yieldBp < 10000 ? ` · about ${formatPercentFromBp(p.yieldBp)} remains after cleaning` : ""}`}
                checked={p.code === preparation}
                disabled={p.chargePaise === null}
                onChange={() => {
                  setPreparation(p.code);
                  setError(null);
                }}
              />
            ))}
          </div>
        </fieldset>
      )}

      {variant.isWeightVariable && product.netWeightNote && (
        <p className="bg-accent-coral-subtle text-accent-coral-text rounded-[10px] px-3 py-2 text-sm">
          {product.netWeightNote}
        </p>
      )}

      {inCart ? (
        <div
          className="bg-surface-paper rounded-[18px] p-4 text-sm shadow-[0_2px_10px_rgb(20_48_47/0.08)]"
          aria-live="polite"
        >
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

      <div className="flex flex-col gap-1">
        <span className="text-text text-sm font-semibold" id="qty-label">
          {variant.isWeightVariable ? "Net weight after cleaning" : "Quantity"}
        </span>
        <p className="text-text-muted text-xs">
          From {formatValue(variant.minOrder.value)} to {formatValue(variant.maxOrder.value)}, in
          steps of {formatValue(variant.orderStep.value)}.
        </p>
      </div>

      {/* The app's add bar: stepper and the teal Add to cart. Sticky on phones, in the column on the website. */}
      <div className="border-border bg-surface-paper fixed inset-x-0 bottom-0 z-[1100] grid grid-cols-[auto_1fr] items-center gap-2.5 border-t px-3 pt-2.5 pb-4 md:static md:border-0 md:bg-transparent md:p-0">
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
        <Button
          size="lg"
          className="h-12 justify-between rounded-2xl px-[18px] text-[15px] font-extrabold"
          leadingIcon={<ShoppingBag aria-hidden="true" />}
          loading={add.isPending}
          disabled={(pincode !== null && !priced) || unavailable}
          onClick={() => void addToCart()}
        >
          <span className="flex flex-1 items-center justify-between gap-3">
            <span>
              {pincode ? (unavailable ? "Not available" : "Add to cart") : "Set pincode to buy"}
            </span>
            {priced && !unavailable && (
              <span aria-hidden="true" className="font-display text-lg font-extrabold">
                {formatRupees(variant.unitPricePaise!)}
                <span className="text-xs font-semibold opacity-80">
                  {" "}
                  {sellUnitLabels[variant.sellUnit]}
                </span>
              </span>
            )}
          </span>
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
