import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Delivery policy",
  alternates: { canonical: "/support/delivery-policy" },
};

// OWNER TEXT: the store owner's final delivery policy replaces these sections.
export default function DeliveryPolicyPage() {
  return (
    <PolicyPage
      title="Delivery policy"
      sections={[
        {
          heading: "Where we deliver",
          body: "Enter your pincode to check whether we deliver to you. The areas we serve can change over time.",
        },
        {
          heading: "Delivery slots",
          body: "You choose a delivery slot in your cart or at checkout. Only slots that are still open are shown, and full slots cannot be picked.",
        },
        {
          heading: "Delivery fees and minimum order",
          body: "Any delivery fee or minimum order for your area is shown in your cart and at checkout before you place the order.",
        },
        {
          heading: "Receiving your order",
          body: "When your order is on its way, your order page shows a short delivery code. Share it with the delivery partner when you receive your order.",
        },
      ]}
    />
  );
}
