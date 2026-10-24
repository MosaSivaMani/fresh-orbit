import { formatRupees } from "@fresh/core";
import { BillBox, type BillRow } from "@fresh/ui";
import { type CartCoupon, type CartTotals } from "@/api/types/cart";

/**
 * The bill box with totals exactly as the server sent them (docs/api §5.2). This component formats paise; it never
 * adds, subtracts or derives an amount.
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
  const rows: BillRow[] = [{ id: "items", label: "Items", value: formatRupees(totals.itemsPaise) }];
  if (totals.preparationPaise > 0) {
    rows.push({
      id: "prep",
      label: "Cleaning and cutting",
      value: formatRupees(totals.preparationPaise),
    });
  }
  if (totals.discountPaise > 0) {
    rows.push({
      id: "discount",
      label: coupon ? `Coupon ${coupon.code}` : "Discount",
      value: `− ${formatRupees(totals.discountPaise)}`,
      tone: "saving",
    });
  }
  rows.push({
    id: "delivery",
    label: "Delivery fee",
    value:
      totals.deliveryFeeWaived || totals.deliveryFeePaise === 0
        ? "Free"
        : formatRupees(totals.deliveryFeePaise),
  });

  const weighed = totals.maxTotalPaise > totals.estimatedTotalPaise;
  const freeDeliveryShort =
    totals.freeDeliveryShortfallPaise != null && totals.freeDeliveryShortfallPaise > 0;
  const minOrderShort =
    totals.minOrderShortfallPaise != null &&
    totals.minOrderShortfallPaise > 0 &&
    totals.minOrderPaise != null;

  return (
    <BillBox
      rows={rows}
      total={{ label: totalLabel, value: formatRupees(totals.estimatedTotalPaise) }}
      notes={
        (weighed || totals.taxPaise > 0 || freeDeliveryShort || minOrderShort) && (
          <>
            {weighed && (
              <p>
                Weighed items are charged on actual net weight. You will pay at most{" "}
                <span className="text-text font-semibold">
                  {formatRupees(totals.maxTotalPaise)}
                </span>
                .
              </p>
            )}
            {totals.taxPaise > 0 && <p>Prices include taxes of {formatRupees(totals.taxPaise)}.</p>}
            {freeDeliveryShort && (
              <p className="bg-teal-soft text-accent-text rounded-lg p-2 font-semibold">
                Add {formatRupees(totals.freeDeliveryShortfallPaise!)} more for free delivery.
              </p>
            )}
            {minOrderShort && (
              <p className="bg-highlight-subtle text-highlight-text rounded-lg p-2 font-semibold">
                The minimum order here is {formatRupees(totals.minOrderPaise!)}. Add{" "}
                {formatRupees(totals.minOrderShortfallPaise!)} more to check out.
              </p>
            )}
          </>
        )
      }
    />
  );
}
