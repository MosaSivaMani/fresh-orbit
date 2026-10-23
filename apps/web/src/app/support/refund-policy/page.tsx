import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Refund policy",
  alternates: { canonical: "/support/refund-policy" },
};

export default function RefundPolicyPage() {
  return (
    <PolicyPage
      title="Refund policy"
      sections={[
        {
          heading: "Reporting a problem",
          outline:
            "Explain how and within how long after delivery to report missing, damaged, wrong or poor-quality items, and that photos help.",
        },
        {
          heading: "Weight differences",
          outline:
            "Explain that weighed items are charged on actual net weight up to the maximum shown at checkout.",
        },
        {
          heading: "How refunds are paid",
          outline:
            "Explain refund methods for orders paid on delivery (for example, UPI transfer or store credit) and the expected time.",
        },
      ]}
    />
  );
}
