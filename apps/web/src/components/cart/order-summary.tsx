import { formatRupees } from "@fresh/core";
import { type ReactNode } from "react";
import { type CartCoupon, type CartTotals } from "@/api/types/cart";

function Row({
  label,
  value,
  emphasis = false,
}: {
  label: ReactNode;
  value: ReactNode;
  emphasis?: boolean;
}) {
  return (
    <div
      className={
        emphasis
          ? "text-text flex justify-between gap-4 text-base font-bold"
          : "text-text flex justify-between gap-4 text-sm"
      }
    >
      <dt>{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

/**
 * Totals exactly as the server sent them (docs/api §5.2). This component formats paise; it never adds, subtracts or
 * derives an amount.
 */
export function OrderSummary({
  totals,
  coupon,
  totalLabel = "Estimated total",
}: {
  totals: CartTotals;
  coupon: CartCoupon | null;
  totalLabel?: string;
}) {
  return (
    <div className="flex flex-col gap-3">
      <dl className="flex flex-col gap-2">
        <Row label="Items" value={formatRupees(totals.itemsPaise)} />
        {totals.preparationPaise > 0 && (
          <Row label="Cleaning and cutting" value={formatRupees(totals.preparationPaise)} />
        )}
        {totals.discountPaise > 0 && (
          <Row
            label={coupon ? `Coupon ${coupon.code}` : "Discount"}
            value={
              <span className="text-success-text">− {formatRupees(totals.discountPaise)}</span>
            }
          />
        )}
        <Row
          label="Delivery fee"
          value={
            totals.deliveryFeeWaived || totals.deliveryFeePaise === 0
              ? "Free"
              : formatRupees(totals.deliveryFeePaise)
          }
        />
        <div className="border-border my-1 border-t" />
        <Row label={totalLabel} value={formatRupees(totals.estimatedTotalPaise)} emphasis />
      </dl>
      {totals.maxTotalPaise > totals.estimatedTotalPaise && (
        <p className="text-text-muted text-xs">
          Weighed items are charged on actual net weight. You will pay at most{" "}
          <span className="text-text font-semibold">{formatRupees(totals.maxTotalPaise)}</span>.
        </p>
      )}
      {totals.taxPaise > 0 && (
        <p className="text-text-muted text-xs">
          Prices include taxes of {formatRupees(totals.taxPaise)}.
        </p>
      )}
      {totals.freeDeliveryShortfallPaise != null && totals.freeDeliveryShortfallPaise > 0 && (
        <p className="bg-accent-subtle text-accent-text rounded-lg p-2 text-xs">
          Add {formatRupees(totals.freeDeliveryShortfallPaise)} more for free delivery.
        </p>
      )}
      {totals.minOrderShortfallPaise != null &&
        totals.minOrderShortfallPaise > 0 &&
        totals.minOrderPaise != null && (
          <p className="bg-highlight-subtle text-highlight-text rounded-lg p-2 text-xs">
            The minimum order here is {formatRupees(totals.minOrderPaise)}. Add{" "}
            {formatRupees(totals.minOrderShortfallPaise)} more to check out.
          </p>
        )}
    </div>
  );
}
