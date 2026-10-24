// Admin coupons and home banners (docs/api §4.13), generated from the backend OpenAPI document.

import type { Id, Schema } from "./common";

export type CouponStatus = Schema<"CouponStatus">;
export type DiscountType = Schema<"DiscountType">;
export type CouponScopeType = Schema<"CouponScopeType">;

export type CouponScope = Schema<"CouponScopeDto">;

/**
 * `discountValue`: PERCENT in basis points (1–10000), FLAT in paise, FREE_DELIVERY 0. `cityId` null = every city;
 * `validTo` null = open-ended; `isListed` shows the coupon on the customer's coupon list.
 */
export type Coupon = Schema<"CouponResponse">;

export interface CouponListQuery {
  status?: string;
  cityId?: Id;
  q?: string;
  page?: number;
  pageSize?: number;
}

export type CouponRequest = Schema<"CouponRequest">;

export type RedemptionStatus = Schema<"RedemptionStatus">;

/** No order number in the response; the screen links the order id. */
export type CouponRedemption = Schema<"RedemptionResponse">;

export type PromotionLinkType = Schema<"PromotionLinkType">;

/** Home banner (`GET/POST/PUT /admin/promotions`, `catalog.manage`). */
export type Promotion = Schema<"PromotionResponse">;

export type PromotionRequest = Schema<"PromotionRequest">;
