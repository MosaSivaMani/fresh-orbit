import type { Metadata } from "next";
import { PolicyPage } from "@/components/policy-page";

export const metadata: Metadata = {
  title: "Privacy policy",
  alternates: { canonical: "/support/privacy-policy" },
};

// OWNER TEXT: the store owner's final privacy policy (docs/legal/privacy-policy.md, once a lawyer approves it)
// replaces these sections. They describe only what the site does today.
export default function PrivacyPolicyPage() {
  return (
    <PolicyPage
      title="Privacy policy"
      sections={[
        {
          heading: "What we keep",
          body: "Your mobile number to sign you in, the name and optional email you give us, your delivery addresses, your orders, and the problems you report with their photos.",
        },
        {
          heading: "Why we keep it",
          body: "To deliver your orders, send you order updates, answer your complaints and keep the records the law asks for. Offers are sent only if you turn them on in Settings.",
        },
        {
          heading: "Cookies",
          body: "The site uses only the cookies it needs to work: your pincode, your guest cart and your sign-in. There are no advertising cookies.",
        },
        {
          heading: "Your choices",
          body: "Change your details and addresses on your account page, choose how we contact you in Settings, or delete your account there.",
        },
      ]}
    />
  );
}
