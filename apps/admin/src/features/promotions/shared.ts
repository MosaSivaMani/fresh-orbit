import { formatPercentFromBp, formatRupees } from "@fresh/core";
import { type Coupon } from "../../api/types/promotions";

/** What a home banner opens when tapped (AD-6); codes are the API's `linkType`. */
export const LINK_TYPE_LABELS: Readonly<Record<string, string>> = {
  CATEGORY: "Category",
  PRODUCT: "Product",
  COUPON: "Coupon",
};

/** Display of the server's stored discount (bp for PERCENT, paise for FLAT); no amounts are computed here. */
export function discountLabel(
  c: Pick<Coupon, "discountType" | "discountValue" | "maxDiscountPaise">,
): string {
  if (c.discountType === "PERCENT")
    return `${formatPercentFromBp(c.discountValue)} off${
      c.maxDiscountPaise != null ? `, up to ${formatRupees(c.maxDiscountPaise)}` : ""
    }`;
  if (c.discountType === "FLAT") return `${formatRupees(c.discountValue)} off`;
  return "Free delivery";
}
