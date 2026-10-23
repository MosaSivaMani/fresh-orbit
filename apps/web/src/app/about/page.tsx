import type { Metadata } from "next";
import Link from "next/link";
import { ContentPage, OwnerPlaceholder } from "@/components/content-page";

export const metadata: Metadata = {
  title: "About us",
  description: "Who we are and how we clean, cut and deliver seafood.",
  alternates: { canonical: "/about" },
};

export default function AboutPage() {
  return (
    <ContentPage
      title="About Fresh Orbit"
      intro="Seafood cleaned and cut to your order, delivered in a slot you choose."
      breadcrumbs={[{ href: "/", label: "Home" }, { label: "About us" }]}
    >
      <OwnerPlaceholder>
        Replace this page with the store&apos;s own story, sourcing partners and team. Do not make
        claims about freshness, catch times or delivery speed that the store cannot back up.
      </OwnerPlaceholder>
      <h2>How ordering works</h2>
      <ul>
        <li>Set your delivery pincode to see what is sold near you and at what price.</li>
        <li>Choose the size and how you want it cleaned and cut.</li>
        <li>
          Weighed items are charged on the actual net weight after cleaning, never above the maximum
          shown in your cart.
        </li>
        <li>Pick a delivery slot and pay on delivery.</li>
      </ul>
      <h2>Questions?</h2>
      <p>
        See our{" "}
        <Link
          href="/support"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
        >
          help and FAQ
        </Link>
        .
      </p>
    </ContentPage>
  );
}
