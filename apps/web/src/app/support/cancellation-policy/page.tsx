import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Cancellation policy",
  alternates: { canonical: "/support/cancellation-policy" },
};

export default function CancellationPolicyPage() {
  return (
    <PolicyPage
      title="Cancellation policy"
      sections={[
        {
          heading: "Cancelling your order",
          outline:
            "Explain until which order stage a customer can cancel from the order page, and how to ask for help after that.",
        },
        {
          heading: "When we cancel",
          outline:
            "Explain when the store may cancel an order (for example, an item becomes unavailable) and how the customer is told.",
        },
        {
          heading: "Charges",
          outline:
            "State whether any cancellation charge applies. With pay on delivery, usually none.",
        },
      ]}
    />
  );
}
