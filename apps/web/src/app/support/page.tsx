import type { Metadata } from "next";
import Link from "next/link";
import { ContentPage, OwnerPlaceholder } from "@/components/content-page";

export const metadata: Metadata = {
  title: "Help and FAQ",
  description:
    "Answers about ordering, weights, delivery slots, payment, cancellation and refunds.",
  alternates: { canonical: "/support" },
};

const FAQ: { q: string; a: React.ReactNode }[] = [
  {
    q: "Why do prices need my pincode?",
    a: "Prices and availability are set per store. Once you enter your pincode we show what the store that delivers to you sells, at its price.",
  },
  {
    q: "Why is my total an estimate?",
    a: "Weighed items are cleaned and weighed when your order is packed. You pay for the actual net weight, which can be a little more or less than ordered, but never above the maximum shown in your cart and at checkout.",
  },
  {
    q: "What does the preparation option change?",
    a: "Cleaning and cutting removes part of the fish. You order the net weight you want; some preparations carry a small charge, shown next to the option.",
  },
  {
    q: "How do I pay?",
    a: "Pay on delivery, by cash or UPI. The amount due is shown on your order once it is packed.",
  },
  {
    q: "Can I cancel my order?",
    a: (
      <>
        Open the order from{" "}
        <Link
          href="/account/orders"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
        >
          your orders
        </Link>
        . If it can still be cancelled you will see a Cancel button. See the{" "}
        <Link
          href="/support/cancellation-policy"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
        >
          cancellation policy
        </Link>
        .
      </>
    ),
  },
  {
    q: "Something was wrong with my order. What do I do?",
    a: "Open the delivered order and choose Report a problem. You can add photos. Complaints can be raised for a limited time after delivery.",
  },
  {
    q: "What is the delivery code?",
    a: "When your order is out for delivery, the order page shows a short code. Share it with the delivery partner when you receive your order.",
  },
];

export default function SupportPage() {
  return (
    <ContentPage
      title="Help and FAQ"
      breadcrumbs={[{ href: "/", label: "Home" }, { label: "Help and FAQ" }]}
    >
      <div className="flex flex-col gap-3">
        {FAQ.map((item) => (
          <details key={item.q} className="border-border bg-surface group rounded-xl border p-4">
            <summary className="text-text cursor-pointer font-semibold">{item.q}</summary>
            <div className="text-text-muted mt-2">{item.a}</div>
          </details>
        ))}
      </div>
      <h2>Policies</h2>
      <ul>
        <li>
          <Link
            href="/support/delivery-policy"
            className="text-accent-text underline-offset-4 hover:underline"
          >
            Delivery policy
          </Link>
        </li>
        <li>
          <Link
            href="/support/cancellation-policy"
            className="text-accent-text underline-offset-4 hover:underline"
          >
            Cancellation policy
          </Link>
        </li>
        <li>
          <Link
            href="/support/refund-policy"
            className="text-accent-text underline-offset-4 hover:underline"
          >
            Refund policy
          </Link>
        </li>
      </ul>
      <h2>Contact us</h2>
      <OwnerPlaceholder>
        Add the store&apos;s support phone number, WhatsApp, email and hours here. There is no
        public business-profile endpoint yet, so this is static text.
      </OwnerPlaceholder>
    </ContentPage>
  );
}
