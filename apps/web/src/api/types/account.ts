// Profile, wishlist, notifications and visible coupons (docs/api §4.2, §4.7, §4.10), generated from the backend
// OpenAPI document (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";
import { type Coded } from "./common";

export type Profile = Coded<Schema<"ProfileResponse">, { status: "ACTIVE" | "DELETED" }>;

/** `PATCH /me`: null leaves a field unchanged (CUS-R2). */
export type UpdateProfileRequest = Partial<Schema<"UpdateProfileRequest">>;

/** `GET /me/wishlist`: id, date and a priced product summary per item (gap W5 closed). */
export type WishlistItem = Schema<"WishlistItemResponse">;

export type InboxItem = Schema<"InboxItem">;

export type InboxResponse = Schema<"InboxResponse">;

export type PreferenceCategory = "ORDER_UPDATES" | "OFFERS";
export type PreferenceChannel = "SMS" | "EMAIL" | "PUSH" | "IN_APP";

export type PreferenceItem = Coded<
  Schema<"PreferenceItem">,
  { category: PreferenceCategory; channel: PreferenceChannel }
>;

export interface PreferencesResponse {
  preferences: PreferenceItem[];
}

export type VisibleCoupon = Schema<"VisibleCoupon">;
