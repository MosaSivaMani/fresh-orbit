import { PageBand, Script } from "@fresh/ui";
import type { Metadata } from "next";
import { CartView } from "@/components/cart/cart-view";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Your cart", robots: { index: false } };

/** Client-rendered: the cart belongs to one visitor (cookie or session) and is never cached or indexed. */
export default function CartPage() {
  return (
    <>
      <PageBand
        title={
          <>
            Your <Script>cart</Script>
          </>
        }
        description="Cleaned your way, priced by the shop for your pincode."
      />
      <Container className="py-6 sm:py-8">
        <CartView />
      </Container>
    </>
  );
}
