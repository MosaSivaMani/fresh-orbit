import type { Metadata } from "next";
import { CartView } from "@/components/cart/cart-view";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Your cart", robots: { index: false } };

/** Client-rendered: the cart belongs to one visitor (cookie or session) and is never cached or indexed. */
export default function CartPage() {
  return (
    <Container className="flex flex-col gap-6 py-8">
      <h1 className="text-text text-3xl font-bold">Your cart</h1>
      <CartView />
    </Container>
  );
}
