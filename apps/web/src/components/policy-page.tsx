import { Highlight } from "@fresh/ui";
import Link from "next/link";
import { ContentPage } from "./content-page";

const policies = [
  { href: "/support/delivery-policy", label: "Delivery policy" },
  { href: "/support/cancellation-policy", label: "Cancellation policy" },
  { href: "/support/refund-policy", label: "Refund policy" },
] as const;

/**
 * A policy page in plain, neutral customer text. Nothing here is legally binding wording: no percentages, timelines
 * or charges the server does not provide. OWNER TEXT: the store owner's final, approved policy wording replaces the
 * `sections` passed by each policy page (search for "OWNER TEXT"). The first word of the title sits on the brush;
 * the side card links the other policies and help.
 */
export function PolicyPage({
  title,
  sections,
}: {
  title: string;
  sections: { heading: string; body: string }[];
}) {
  const [first = title, ...rest] = title.split(" ");
  return (
    <ContentPage
      title={
        <>
          <Highlight>{first}</Highlight>
          {rest.length > 0 && ` ${rest.join(" ")}`}
        </>
      }
      breadcrumbs={[
        { href: "/", label: "Home" },
        { href: "/support", label: "Help" },
        { label: title },
      ]}
      aside={
        <nav
          aria-label="Policies"
          className="bg-surface-sand flex flex-col gap-2 rounded-[20px] p-5 text-sm"
        >
          <h2 className="font-display text-text text-lg font-extrabold">Other policies</h2>
          {policies
            .filter((p) => p.label !== title)
            .map((p) => (
              <Link
                key={p.href}
                href={p.href}
                className="text-accent-text w-fit font-semibold underline-offset-4 hover:underline"
              >
                {p.label}
              </Link>
            ))}
          <Link
            href="/support"
            className="text-accent-text w-fit font-semibold underline-offset-4 hover:underline"
          >
            Help and FAQ
          </Link>
        </nav>
      }
    >
      {sections.map((s) => (
        <section key={s.heading} className="flex flex-col gap-2">
          <h2>{s.heading}</h2>
          <p className="text-text-muted">{s.body}</p>
        </section>
      ))}
    </ContentPage>
  );
}
