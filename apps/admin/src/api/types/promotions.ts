// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.13 (admin coupons); docs/product/promotions.md (PROMO-R19…R22, "API gaps": fields are the
// table columns in camelCase + scopes, usedCount, version). The Promotions module had no endpoints on `main` when
// these were written.

import type { Id, Instant, Version } from "./common";

export type CouponStatus = "ACTIVE" | "PAUSED" | "EXPIRED";
export type DiscountType = "PERCENT" | "FLAT" | "FREE_DELIVERY";
export type CouponScopeType = "CATEGORY" | "PRODUCT";

export interface CouponScope {
  scopeType: CouponScopeType;
  scopeId: Id;
}

export interface Coupon {
  id: Id;
  code: string;
  description: string;
  discountType: DiscountType;
  /** PERCENT: basis points (1–10000); FLAT: paise; FREE_DELIVERY: 0. */
  discountValue: number;
  maxDiscountPaise: number | null;
  minOrderPaise: number;
  /** null = every city. */
  cityId: Id | null;
  firstOrderOnly: boolean;
  perCustomerLimit: number | null;
  totalLimit: number | null;
  usedCount: number;
  validFrom: Instant;
  validTo: Instant;
  status: CouponStatus;
  scopes?: CouponScope[];
  version: Version;
}

export interface CouponListQuery {
  status?: string;
  cityId?: Id;
  q?: string;
  page?: number;
  pageSize?: number;
}

export interface CouponRequest {
  code: string;
  description: string;
  discountType: DiscountType;
  discountValue: number;
  maxDiscountPaise: number | null;
  minOrderPaise: number;
  cityId: Id | null;
  firstOrderOnly: boolean;
  perCustomerLimit: number | null;
  totalLimit: number | null;
  validFrom: Instant;
  validTo: Instant;
  scopes: CouponScope[];
}

export interface CouponRedemption {
  id: Id;
  orderId: Id;
  orderNumber?: string | null;
  customerId: Id;
  discountPaise: number;
  status: "APPLIED" | "REVERSED";
  redeemedAt: Instant;
}
