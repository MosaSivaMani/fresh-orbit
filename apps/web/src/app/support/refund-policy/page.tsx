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
          body: "Weighed items are priced on net weight after cleaning, and the price you see is the most you pay. If your pack weighs less than you ordered, you pay less on delivery, or we refund the difference if you paid online. If it weighs more, the extra is free.",
        },
        {
          heading: "How we resolve it",
          body: "We review every report and reply on your order page with what we can do for you.",
        },
      ]}
    />
  );
}
