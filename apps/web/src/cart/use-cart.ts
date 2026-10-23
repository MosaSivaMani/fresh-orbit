"use client";

// Cart state = the server's priced cart (CART-R1). Every write returns the full cart, which replaces the cached one;
// nothing is calculated here.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { browserHttp } from "@/api/browser";
import {
  addCartItem,
  applyCoupon,
  changeCartItem,
  changeCartLocation,
  getCart,
  removeCartItem,
  removeCoupon,
} from "@/api/endpoints/cart";
import { type AddCartItemRequest, type Cart, type ChangeCartItemRequest } from "@/api/types/cart";
import { useSession } from "@/auth/use-session";
import { queryKeys } from "@/lib/query-keys";

export function useCart() {
  const session = useSession();
  return useQuery({
    queryKey: queryKeys.cart,
    queryFn: () => getCart(browserHttp()),
    // Wait until we know whether the visitor is signed in: a customer's cart and a guest cart differ.
    enabled: session.status !== "unknown",
    staleTime: 10_000,
  });
}

function useCartWrite<TVariables>(write: (variables: TVariables) => Promise<Cart>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: (cart) => queryClient.setQueryData(queryKeys.cart, cart),
  });
}

export function useAddToCart() {
  return useCartWrite((input: { body: AddCartItemRequest; pincode: string | null }) =>
    addCartItem(browserHttp(), input.body, input.pincode),
  );
}

export function useChangeCartItem() {
  return useCartWrite((input: { itemId: string; body: ChangeCartItemRequest }) =>
    changeCartItem(browserHttp(), input.itemId, input.body),
  );
}

export function useRemoveCartItem() {
  return useCartWrite((itemId: string) => removeCartItem(browserHttp(), itemId));
}

export function useApplyCoupon() {
  return useCartWrite((code: string) => applyCoupon(browserHttp(), code));
}

export function useRemoveCoupon() {
  return useCartWrite(() => removeCoupon(browserHttp()));
}

export function useChangeCartLocation() {
  return useCartWrite((input: { pincode: string; force: boolean }) =>
    changeCartLocation(browserHttp(), input.pincode, input.force),
  );
}
