import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Refund policy",
  alternates: { canonical: "/support/refund-policy" },
};

// OWNER TEXT: the store owner's final refund policy replaces these sections.
export default function RefundPolicyPage() {
  return (
    <PolicyPage
      title="Refund policy"
      sections={[
        {
          heading: "Reporting a problem",
          body: "If something is missing, damaged or not right, open the delivered order and choose Report a problem. Photos help us look into it.",
        },
        {
          heading: "Weight differences",
          body: "Weighed items are charged on their actual net weight after cleaning, never above the maximum shown in your cart and at checkout.",
        },
        {
          heading: "How we resolve it",
          body: "We review every report and reply on your order page with what we can do for you.",
        },
      ]}
    />
  );
}
