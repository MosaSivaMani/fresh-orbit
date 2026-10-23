import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { CheckoutView } from "@/components/checkout/checkout-view";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Checkout", robots: { index: false } };

export default function CheckoutPage() {
  return (
    <Container className="flex flex-col gap-6 py-8">
      <h1 className="text-text text-3xl font-bold">Checkout</h1>
      <RequireAuth>
        <CheckoutView />
      </RequireAuth>
    </Container>
  );
}
