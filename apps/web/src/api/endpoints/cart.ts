// Cart endpoints (docs/api §4.6). Every write returns the full priced cart.
import { type HttpClient } from "@fresh/core";
import { type AddCartItemRequest, type Cart, type ChangeCartItemRequest } from "../types/cart";

export function getCart(http: HttpClient): Promise<Cart> {
  return http.get<Cart>("/cart");
}

/** `pincode` is sent as `X-Pincode`; the server uses it only for the first add (cart has no pincode yet). */
export function addCartItem(
  http: HttpClient,
  body: AddCartItemRequest,
  pincode?: string | null,
): Promise<Cart> {
  return http.post<Cart>("/cart/items", body, pincode ? { headers: { "X-Pincode": pincode } } : {});
}

export function changeCartItem(
  http: HttpClient,
  itemId: string,
  body: ChangeCartItemRequest,
): Promise<Cart> {
  return http.patch<Cart>(`/cart/items/${encodeURIComponent(itemId)}`, body);
}

export function removeCartItem(http: HttpClient, itemId: string): Promise<Cart> {
  return http.delete<Cart>(`/cart/items/${encodeURIComponent(itemId)}`);
}

export function clearCart(http: HttpClient): Promise<Cart> {
  return http.delete<Cart>("/cart");
}

/** 409 `CART_LOCATION_CHANGED` lists lines that would drop; call again with `force` to confirm. */
export function changeCartLocation(
  http: HttpClient,
  pincode: string,
  force = false,
): Promise<Cart> {
  return http.put<Cart>("/cart/location", { pincode }, force ? { query: { force: true } } : {});
}

export function applyCoupon(http: HttpClient, code: string): Promise<Cart> {
  return http.put<Cart>("/cart/coupon", { code });
}

export function removeCoupon(http: HttpClient): Promise<Cart> {
  return http.delete<Cart>("/cart/coupon");
}
