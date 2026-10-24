import { PageBand, Script } from "@fresh/ui";
import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { CheckoutView } from "@/components/checkout/checkout-view";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Checkout", robots: { index: false } };

/** `?slot=` carries the slot chosen in the cart; checkout's server preview decides whether it still holds. */
export default async function CheckoutPage(props: PageProps<"/checkout">) {
  const { slot } = await props.searchParams;
  return (
    <>
      <PageBand
        title={
          <>
            Almost <Script>there</Script>
          </>
        }
        description="Choose where and when, and pay when your fish arrives."
      />
      <Container className="py-6 sm:py-8">
        <RequireAuth>
          <CheckoutView initialSlotId={typeof slot === "string" && slot ? slot : null} />
        </RequireAuth>
      </Container>
    </>
  );
}
