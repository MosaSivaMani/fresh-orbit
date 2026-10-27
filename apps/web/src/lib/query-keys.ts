// TanStack Query keys, in one place so mutations invalidate the right data.
export const queryKeys = {
  cart: ["cart"] as const,
  serviceability: (pincode: string) => ["serviceability", pincode] as const,
  product: (slug: string, preparation: string | undefined, pincode: string | null) =>
    ["product", slug, preparation ?? null, pincode] as const,
  suggest: (q: string) => ["suggest", q] as const,
  priceQuote: (
    variantId: string,
    preparation: string | undefined,
    quantity: number,
    pincode: string | null,
  ) => ["price-quote", variantId, preparation ?? null, quantity, pincode] as const,
  profile: ["me"] as const,
  addresses: ["me", "addresses"] as const,
  wishlist: ["me", "wishlist"] as const,
  stockAlerts: ["me", "stock-alerts"] as const,
  notifications: (page: number) => ["me", "notifications", page] as const,
  preferences: ["me", "preferences"] as const,
  orders: (page: number) => ["orders", page] as const,
  order: (orderId: string) => ["order", orderId] as const,
  tracking: (orderId: string) => ["order", orderId, "tracking"] as const,
  complaints: (orderId: string) => ["order", orderId, "complaints"] as const,
  slots: (pincode: string) => ["slots", pincode] as const,
  coupons: ["coupons"] as const,
  promotions: (pincode: string | null) => ["promotions", pincode] as const,
};
