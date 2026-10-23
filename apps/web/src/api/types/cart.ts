// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §1.3, §4.6, §5 and docs/product/cart.md.

import { type Availability, type Issue, type Quantity, type SellUnit } from "./common";

export interface CartLine {
  id: string;
  productId: string;
  productSlug: string;
  productName: string;
  variantId: string;
  variantName: string;
  sellUnit: SellUnit;
  imageUrl: string | null;
  preparation: { code: string; name: string; yieldBp: number };
  quantity: Quantity;
  estimatedNetQuantity: Quantity;
  maxChargeableQuantity: Quantity;
  unitPricePaise: number | null;
  compareAtPaise: number | null;
  priceUnit: SellUnit;
  itemPaise: number;
  preparationChargePaise: number;
  preparationChargeBasis: string | null;
  taxRateBp: number;
  taxPaise: number;
  estimatedLineTotalPaise: number;
  maxLineTotalPaise: number;
  availability: Availability;
  isSellable: boolean;
  issues: Issue[];
}

export type CouponStatus = "APPLIED" | "NOT_APPLICABLE" | "INVALID";

export interface CartCoupon {
  code: string;
  status: CouponStatus;
  discountPaise: number;
  message: string | null;
}

export interface CartTotals {
  itemsPaise: number;
  preparationPaise: number;
  subtotalPaise: number;
  discountPaise: number;
  deliveryFeePaise: number;
  deliveryFeeWaived: boolean;
  taxPaise: number;
  estimatedTotalPaise: number;
  maxTotalPaise: number;
  minOrderPaise?: number;
  minOrderShortfallPaise?: number;
  freeDeliveryShortfallPaise?: number | null;
}

export interface Cart {
  id: string | null;
  /** Issued once to a guest; the website keeps it in an httpOnly cookie and blanks it here. */
  cartToken: string | null;
  pincode: string | null;
  fulfillmentLocationCode: string | null;
  lines: CartLine[];
  coupon: CartCoupon | null;
  totals: CartTotals;
  canCheckout: boolean;
  checkoutBlockers: Issue[];
  messages: string[];
}

export interface AddCartItemRequest {
  variantId: string;
  preparationCode?: string;
  quantity: Quantity;
}

export interface ChangeCartItemRequest {
  quantity?: Quantity;
  preparationCode?: string;
}

/** Problem Details member of 409 `CART_LOCATION_CHANGED` (cart.md "API gaps": `lines: [{ id, code }]`). */
export interface DroppedCartLine {
  id: string;
  code: string;
}
