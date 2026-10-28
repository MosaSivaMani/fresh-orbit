import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Terms of use",
  alternates: { canonical: "/support/terms" },
};

// OWNER TEXT: the store owner's final terms (docs/legal/terms-of-use.md, once a lawyer approves them) replace these
// sections. They describe only what the site does today.
export default function TermsPage() {
  return (
    <PolicyPage
      title="Terms of use"
      sections={[
        {
          heading: "Prices",
          body: "Prices depend on where we deliver, so we show them once you enter your pincode. The total at checkout is the one we charge, never more than the maximum shown for weighed items.",
        },
        {
          heading: "Orders",
          body: "Your order is placed when you see the order number. If prices change while you check out, we show the new total and ask you to confirm it.",
        },
        {
          heading: "Paying",
          body: "You pay on delivery, in cash or by UPI, for the amount shown on your order.",
        },
        {
          heading: "Cancelling and problems",
          body: "Our cancellation and refund policies explain when you can cancel and how to report a problem with a delivered order.",
        },
      ]}
    />
  );
}
