import { Highlight } from "@fresh/ui";
import { Clock, Mail, Phone } from "lucide-react";
import type { Metadata } from "next";
import Link from "next/link";
import { getBusinessProfile } from "@/api/endpoints/catalog";
import { publicHttp } from "@/api/server";
import { type PublicBusinessProfile } from "@/api/types/locations";
import { ContentPage } from "@/components/content-page";
import { GrievanceOfficerCard } from "@/components/grievance-officer";
import { getGrievanceOfficer } from "@/server/config";

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

/** The store's contact details, cached like the footer's cities; null when the API is unavailable. */
async function loadProfile(): Promise<PublicBusinessProfile | null> {
  try {
    return await getBusinessProfile(publicHttp(300));
  } catch (error) {
    console.warn(
      "Help: business profile unavailable.",
      error instanceof Error ? error.message : error,
    );
    return null;
  }
}

/** Phone, email and hours exactly as the store saved them (`GET /business-profile`, gap W7). */
function ContactCard({ profile }: { profile: PublicBusinessProfile | null }) {
  return (
    <section
      id="contact"
      aria-labelledby="contact-heading"
      className="bg-teal-soft flex flex-col gap-3 rounded-[20px] p-5 text-sm"
    >
      <h2 id="contact-heading" className="font-display text-text text-lg font-extrabold">
        Contact us
      </h2>
      {profile ? (
        <ul className="text-text flex flex-col gap-2">
          <li className="flex items-center gap-2">
            <Phone aria-hidden="true" className="text-accent-text size-4 shrink-0" />
            <a
              href={`tel:${profile.supportPhone.replace(/\s+/g, "")}`}
              className="font-semibold underline-offset-4 hover:underline"
            >
              {profile.supportPhone}
            </a>
          </li>
          <li className="flex items-center gap-2">
            <Mail aria-hidden="true" className="text-accent-text size-4 shrink-0" />
            <a
              href={`mailto:${profile.supportEmail}`}
              className="font-semibold break-all underline-offset-4 hover:underline"
            >
              {profile.supportEmail}
            </a>
          </li>
          {profile.supportHours && (
            <li className="flex items-center gap-2">
              <Clock aria-hidden="true" className="text-accent-text size-4 shrink-0" />
              {profile.supportHours}
            </li>
          )}
        </ul>
      ) : (
        <p className="text-text-muted">
          Our contact details could not be loaded. For an order, open it from{" "}
          <Link
            href="/account/orders"
            className="text-accent-text font-semibold underline-offset-4 hover:underline"
          >
            your orders
          </Link>{" "}
          and choose Report a problem.
        </p>
      )}
    </section>
  );
}

export default async function SupportPage() {
  const profile = await loadProfile();
  // Shown only when configured (L-7, D300); the business profile has no grievance fields yet.
  const officer = getGrievanceOfficer();
  return (
    <ContentPage
      title={
        <>
          Help and <Highlight>FAQ</Highlight>
        </>
      }
      intro="Answers about ordering, weights, slots, payment, cancellation and refunds."
      breadcrumbs={[{ href: "/", label: "Home" }, { label: "Help and FAQ" }]}
      picture={{ slot: "ONBOARDING_DELIVERY", focus: "45% 25%" }}
      aside={
        <div className="flex flex-col gap-4">
          <ContactCard profile={profile} />
          {officer && <GrievanceOfficerCard officer={officer} />}
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {FAQ.map((item) => (
          <details
            key={item.q}
            className="border-border bg-surface group rounded-[20px] border p-4 open:shadow-[0_6px_18px_rgb(10_38_48/0.08)]"
          >
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
        <li>
          <Link
            href="/support/privacy-policy"
            className="text-accent-text underline-offset-4 hover:underline"
          >
            Privacy policy
          </Link>
        </li>
        <li>
          <Link
            href="/support/terms"
            className="text-accent-text underline-offset-4 hover:underline"
          >
            Terms of use
          </Link>
        </li>
      </ul>
    </ContentPage>
  );
}
