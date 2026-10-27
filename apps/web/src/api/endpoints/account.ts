// Account, addresses, wishlist, notifications (docs/api §4.2, §4.7, §4.10). Updates quote `version` with If-Match.
import { type HttpClient } from "@fresh/core";
import {
  type InboxResponse,
  type PreferenceItem,
  type PreferencesResponse,
  type Profile,
  type StockAlert,
  type UpdateProfileRequest,
  type VisibleCoupon,
  type WishlistItem,
} from "../types/account";
import { type OtpChallenge, type OtpRequestBody } from "../types/auth";
import { type Address, type AddressInput } from "../types/checkout";
import { type Page } from "../types/common";

export function requestOtp(http: HttpClient, phone: string): Promise<OtpChallenge> {
  const body: OtpRequestBody = { phone, purpose: "LOGIN" };
  return http.post<OtpChallenge>("/auth/otp/request", body);
}

export function getProfile(http: HttpClient): Promise<Profile> {
  return http.get<Profile>("/me");
}

export function updateProfile(
  http: HttpClient,
  body: UpdateProfileRequest,
  version: string,
): Promise<Profile> {
  return http.patch<Profile>("/me", body, { ifMatch: version });
}

export function deleteAccount(http: HttpClient, version: string): Promise<void> {
  return http.delete<void>("/me", { ifMatch: version });
}

export function listAddresses(http: HttpClient): Promise<Address[]> {
  return http.get<Address[]>("/me/addresses");
}

export function addAddress(http: HttpClient, body: AddressInput): Promise<Address> {
  return http.post<Address>("/me/addresses", body);
}

export function replaceAddress(
  http: HttpClient,
  addressId: string,
  body: AddressInput,
  version: string,
): Promise<Address> {
  return http.put<Address>(`/me/addresses/${encodeURIComponent(addressId)}`, body, {
    ifMatch: version,
  });
}

export function deleteAddress(http: HttpClient, addressId: string): Promise<void> {
  return http.delete<void>(`/me/addresses/${encodeURIComponent(addressId)}`);
}

export function makeDefaultAddress(http: HttpClient, addressId: string): Promise<Address> {
  return http.post<Address>(`/me/addresses/${encodeURIComponent(addressId)}/default`);
}

export function listWishlist(http: HttpClient): Promise<WishlistItem[]> {
  return http.get<WishlistItem[]>("/me/wishlist");
}

export function addToWishlist(http: HttpClient, productId: string): Promise<void> {
  return http.put<void>(`/me/wishlist/${encodeURIComponent(productId)}`);
}

export function removeFromWishlist(http: HttpClient, productId: string): Promise<void> {
  return http.delete<void>(`/me/wishlist/${encodeURIComponent(productId)}`);
}

/** The customer's "notify me when back in stock" alerts. */
export function listStockAlerts(http: HttpClient): Promise<StockAlert[]> {
  return http.get<StockAlert[]>("/me/stock-alerts");
}

/** Asks to be told when a sold-out size is back at the store that serves the pincode. */
export function createStockAlert(
  http: HttpClient,
  variantId: string,
  fulfillmentLocationId: string,
): Promise<StockAlert> {
  return http.post<StockAlert>("/me/stock-alerts", { variantId, fulfillmentLocationId });
}

export function deleteStockAlert(http: HttpClient, alertId: string): Promise<void> {
  return http.delete<void>(`/me/stock-alerts/${encodeURIComponent(alertId)}`);
}

export function listNotifications(
  http: HttpClient,
  params: { unreadOnly?: boolean; page?: number } = {},
): Promise<InboxResponse> {
  return http.get<InboxResponse>("/me/notifications", {
    query: { unreadOnly: params.unreadOnly, page: params.page },
  });
}

export function markNotificationRead(http: HttpClient, messageId: string): Promise<void> {
  return http.post<void>(`/me/notifications/${encodeURIComponent(messageId)}/read`);
}

export function markAllNotificationsRead(http: HttpClient): Promise<void> {
  return http.post<void>("/me/notifications/read-all");
}

export function getPreferences(http: HttpClient): Promise<PreferencesResponse> {
  return http.get<PreferencesResponse>("/me/notification-preferences");
}

/** Replaces the full set (NOT-R8); `ORDER_UPDATES` SMS cannot be disabled (422 `PREFERENCE_REQUIRED`). */
export function replacePreferences(
  http: HttpClient,
  preferences: PreferenceItem[],
): Promise<PreferencesResponse> {
  return http.put<PreferencesResponse>("/me/notification-preferences", { preferences });
}

/**
 * Coupons the signed-in customer can use at the city of `pincode`; one page holds them all. The pincode is sent as
 * `?pincode=`: without it the server answers an empty list.
 */
export function listCoupons(http: HttpClient, pincode: string): Promise<VisibleCoupon[]> {
  const page = http.get<Page<VisibleCoupon>>("/coupons", { query: { pincode } });
  return page.then((data) => data.items);
}
