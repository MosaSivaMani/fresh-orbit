import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Delivery policy",
  alternates: { canonical: "/support/delivery-policy" },
};

export default function DeliveryPolicyPage() {
  return (
    <PolicyPage
      title="Delivery policy"
      sections={[
        {
          heading: "Where we deliver",
          outline:
            "Explain that delivery areas are checked by pincode on the website and may change.",
        },
        {
          heading: "Delivery slots",
          outline:
            "Explain how slots, order cut-off times and full slots work, and what happens if a slot is missed.",
        },
        {
          heading: "Delivery fees and minimum order",
          outline:
            "Explain that the fee and minimum order depend on the area and are shown in the cart before ordering.",
        },
        {
          heading: "Receiving your order",
          outline:
            "Explain the delivery code, what happens if nobody is available, and how cold items are handed over.",
        },
      ]}
    />
  );
}
