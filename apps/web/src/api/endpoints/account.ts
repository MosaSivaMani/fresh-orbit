// Account, addresses, wishlist, notifications (docs/api §4.2, §4.7, §4.10). Updates quote `version` with If-Match.
import { type HttpClient } from "@fresh/core";
import {
  type InboxResponse,
  type PreferenceItem,
  type PreferencesResponse,
  type Profile,
  type UpdateProfileRequest,
  type VisibleCoupon,
  type WishlistItem,
} from "../types/account";
import { type OtpChallenge, type OtpRequestBody } from "../types/auth";
import { type Address, type AddressInput } from "../types/checkout";

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
  return http
    .get<Address[] | { items: Address[] }>("/me/addresses")
    .then((data) => (Array.isArray(data) ? data : data.items));
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
  return http
    .get<WishlistItem[] | { items: WishlistItem[] }>("/me/wishlist")
    .then((data) => (Array.isArray(data) ? data : data.items));
}

export function addToWishlist(http: HttpClient, productId: string): Promise<void> {
  return http.put<void>(`/me/wishlist/${encodeURIComponent(productId)}`);
}

export function removeFromWishlist(http: HttpClient, productId: string): Promise<void> {
  return http.delete<void>(`/me/wishlist/${encodeURIComponent(productId)}`);
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

export function listCoupons(http: HttpClient): Promise<VisibleCoupon[]> {
  return http
    .get<VisibleCoupon[] | { items: VisibleCoupon[] }>("/coupons")
    .then((data) => (Array.isArray(data) ? data : data.items));
}
