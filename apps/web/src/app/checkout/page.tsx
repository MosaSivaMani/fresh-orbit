import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { CheckoutView } from "@/components/checkout/checkout-view";
import { Container } from "@/components/section";
import { Highlight, ThemeBand } from "@/components/theme/theme-band";

export const metadata: Metadata = { title: "Checkout", robots: { index: false } };

/** `?slot=` carries the slot chosen in the cart; checkout's server preview decides whether it still holds. */
export default async function CheckoutPage(props: PageProps<"/checkout">) {
  const { slot } = await props.searchParams;
  return (
    <>
      <ThemeBand
        title={
          <>
            Almost <Highlight>there</Highlight>
          </>
        }
        description="Choose where and when, and pay when your fish arrives."
        picture={{ slot: "ONBOARDING_DELIVERY", focus: "45% 25%" }}
      />
      <Container className="py-6 sm:py-8">
        <RequireAuth>
          <CheckoutView initialSlotId={typeof slot === "string" && slot ? slot : null} />
        </RequireAuth>
      </Container>
    </>
  );
}
