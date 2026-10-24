// Cart shapes (docs/api §4.6), generated from the backend OpenAPI document (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";
import {
  type AvailabilityStatus,
  type Coded,
  type QuantityUnit,
  type RequestBody,
  type SellUnit,
} from "./common";

/** A cart quantity; the spec types `unit` as a string, the server writes GRAM / PIECE / PACK. */
export type CartQuantity = Coded<Schema<"QuantityView">, { unit: QuantityUnit }>;

export type CartLine = Coded<
  Schema<"CartLineView">,
  {
    sellUnit: SellUnit;
    priceUnit: SellUnit;
    quantity: CartQuantity;
    estimatedNetQuantity: CartQuantity;
    maxChargeableQuantity: CartQuantity;
    availability: Coded<Schema<"CartAvailabilityView">, { status: AvailabilityStatus }>;
  }
>;

export type CouponStatus = "APPLIED" | "NOT_APPLICABLE" | "INVALID";

export type CartCoupon = Coded<Schema<"CartCouponView">, { status: CouponStatus }>;

export type CartTotals = Schema<"CartTotalsView">;

/** A reason the cart cannot be checked out (line-level when `lineId` is set). */
export type CartBlocker = Schema<"CartBlockerView">;

export type Cart = Coded<Schema<"CartView">, { lines: CartLine[]; coupon: CartCoupon | null }>;

export type AddCartItemRequest = RequestBody<Schema<"AddCartItemRequest">>;

export type ChangeCartItemRequest = RequestBody<Schema<"ChangeCartItemRequest">>;

/**
 * Problem Details member `lines` of 409 `CART_LOCATION_CHANGED`. Not in the OpenAPI document (Problem Details
 * extensions are untyped there); shape from `CartEndpoints` `PUT /cart/location`.
 */
export interface DroppedCartLine {
  id: string;
  code: string;
}
