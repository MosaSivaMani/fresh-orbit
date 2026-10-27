import type { Metadata } from "next";
import { CartView } from "@/components/cart/cart-view";
import { Container } from "@/components/section";
import { Highlight, ThemeBand } from "@/components/theme/theme-band";

export const metadata: Metadata = { title: "Your cart", robots: { index: false } };

/** Client-rendered: the cart belongs to one visitor (cookie or session) and is never cached or indexed. */
export default function CartPage() {
  return (
    <>
      <ThemeBand
        title={
          <>
            Your <Highlight>cart</Highlight>
          </>
        }
        description="Cleaned your way, priced by the shop for your pincode."
        picture={{ slot: "ONBOARDING_CLEANED", focus: "50% 30%" }}
      />
      <Container className="py-6 sm:py-8">
        <CartView />
      </Container>
    </>
  );
}
