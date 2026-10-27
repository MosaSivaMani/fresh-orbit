import { Button } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";
import { ContentPage, OwnerPlaceholder } from "@/components/content-page";
import { Highlight } from "@/components/theme/theme-band";

export const metadata: Metadata = {
  title: "About us",
  description: "Who we are and how we clean, cut and deliver seafood.",
  alternates: { canonical: "/about" },
};

export default function AboutPage() {
  return (
    <ContentPage
      title={
        <>
          About <Highlight>Fresh Orbitt</Highlight>
        </>
      }
      intro="Seafood cleaned and cut to your order, delivered in a slot you choose."
      breadcrumbs={[{ href: "/", label: "Home" }, { label: "About us" }]}
      picture={{ slot: "ONBOARDING_FRESH", focus: "50% 35%" }}
      aside={
        <div className="bg-teal-soft flex flex-col gap-3 rounded-[20px] p-5">
          <h2 className="font-display text-text text-lg font-extrabold">Ready to order?</h2>
          <p className="text-text-muted text-sm">
            Set your pincode to see what the store near you sells today.
          </p>
          <Button asChild className="self-start">
            <Link href="/shop">Shop fresh fish</Link>
          </Button>
        </div>
      }
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
