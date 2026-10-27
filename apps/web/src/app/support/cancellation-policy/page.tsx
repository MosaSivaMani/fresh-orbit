import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Cancellation policy",
  alternates: { canonical: "/support/cancellation-policy" },
};

// OWNER TEXT: the store owner's final cancellation policy replaces these sections.
export default function CancellationPolicyPage() {
  return (
    <PolicyPage
      title="Cancellation policy"
      sections={[
        {
          heading: "Cancelling your order",
          body: "Open your order from Your orders. While the order can still be cancelled, the page shows a Cancel button. After that, contact us from the help page.",
        },
        {
          heading: "When we cancel",
          body: "If we cannot fulfil your order, for example because an item is no longer available, we cancel it and let you know.",
        },
        {
          heading: "Paying for a cancelled order",
          body: "You pay on delivery, so there is nothing to pay for an order that is cancelled before it reaches you.",
        },
      ]}
    />
  );
}
