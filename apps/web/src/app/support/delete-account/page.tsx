import { Highlight } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";
import { getBusinessProfile } from "@/api/endpoints/catalog";
import { publicHttp } from "@/api/server";
import { type PublicBusinessProfile } from "@/api/types/locations";
import { ContentPage } from "@/components/content-page";

export const metadata: Metadata = {
  title: "Delete your account",
  description:
    "How to delete your account and data, what is deleted and what the law makes us keep.",
  alternates: { canonical: "/support/delete-account" },
};

async function loadProfile(): Promise<PublicBusinessProfile | null> {
  try {
    return await getBusinessProfile(publicHttp(300));
  } catch (error) {
    console.warn(
      "Delete account: business profile unavailable.",
      error instanceof Error ? error.message : error,
    );
    return null;
  }
}

const link = "text-accent-text w-fit font-semibold underline-offset-4 hover:underline";

// OWNER TEXT: the owner's approved wording replaces these texts. They describe what the system does today:
// `DELETE /api/v1/me` (CUS-R7) and the retention table of docs/legal/privacy-policy.md §5.
const TEXT = {
  inApp:
    "In the app, open Account, then Settings, and choose Delete my account. On this website, sign in, open your account settings and choose Delete account. The account is deleted straight away.",
  byEmail:
    "You can also ask us by e-mail, without the app. Write from the e-mail on your account, or give the mobile number you sign in with; we confirm the request with that number and then delete the account.",
  deleted:
    "Your name, e-mail and phone number, your saved addresses, your wishlist and your stock alerts. You can no longer sign in with the account. A cart is cleared 30 days after it was last used.",
  kept: "Your orders and invoices, with the delivery name, phone and address on each order, and the payment and refund records, are kept for 72 months because GST law (CGST Act section 36) requires it. They are then deleted. Of a saved address only the label, city, state and pincode stay.",
  before:
    "An account cannot be deleted while an order is still in progress, or while a complaint or refund is open. Wait until it is finished, then delete the account.",
};

/**
 * Account deletion without the app (launch L-82, D376): the web page Google Play asks for. The support e-mail comes
 * from the business profile; nothing is hard-coded, and without it the page points to the Help page.
 */
export default async function DeleteAccountPage() {
  const profile = await loadProfile();
  const email = profile?.supportEmail.trim();
  return (
    <ContentPage
      title={
        <>
          <Highlight>Delete</Highlight> your account
        </>
      }
      intro="You can delete your account and your personal details at any time, with or without the app."
      breadcrumbs={[
        { href: "/", label: "Home" },
        { href: "/support", label: "Help" },
        { label: "Delete your account" },
      ]}
    >
      <section id="in-the-app" className="flex flex-col gap-2">
        <h2>In the app or on this website</h2>
        <p className="text-text-muted">{TEXT.inApp}</p>
        <Link href="/account/settings" className={link}>Open your account settings</Link>
      </section>
      <section id="by-email" className="flex flex-col gap-2">
        <h2>By e-mail</h2>
        <p className="text-text-muted">{TEXT.byEmail}</p>
        {email ? (
          <a href={`mailto:${email}`} className={link}>{email}</a>
        ) : (
          <Link href="/support#contact" className={link}>
            Find our support e-mail on the Help page
          </Link>
        )}
      </section>
      <section id="deleted" className="flex flex-col gap-2">
        <h2>What is deleted</h2>
        <p className="text-text-muted">{TEXT.deleted}</p>
      </section>
      <section id="kept" className="flex flex-col gap-2">
        <h2>What we keep, and for how long</h2>
        <p className="text-text-muted">{TEXT.kept}</p>
      </section>
      <section id="before" className="flex flex-col gap-2">
        <h2>Before you delete</h2>
        <p className="text-text-muted">{TEXT.before}</p>
        <Link href="/support/privacy-policy" className={link}>Read our privacy policy</Link>
      </section>
    </ContentPage>
  );
}
