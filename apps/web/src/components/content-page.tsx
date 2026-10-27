import { type IllustrationSlot } from "@fresh/ui";
import { type ReactNode } from "react";
import { cx } from "@/lib/cx";
import { Breadcrumbs } from "./breadcrumbs";
import { Container } from "./section";
import { ThemeBand } from "./theme/theme-band";

/**
 * A text page (about, help, policies) in the app's look (D181): the harbour-sky band with the breadcrumbs, the
 * heading (key word in a `Highlight`) and the intro, then the text on a paper card. `aside` is a column beside the
 * card from `lg` up (contact, other policies); it drops under the card on phones.
 */
export function ContentPage({
  title,
  intro,
  breadcrumbs,
  picture,
  aside,
  children,
}: {
  title: ReactNode;
  intro?: ReactNode;
  breadcrumbs: { href?: string; label: string }[];
  picture?: { slot: IllustrationSlot; focus?: string };
  aside?: ReactNode;
  children: ReactNode;
}) {
  return (
    <>
      <ThemeBand
        eyebrow={<Breadcrumbs tone="band" items={breadcrumbs} />}
        title={title}
        description={intro}
        picture={picture}
      />
      <Container
        className={cx(
          "grid items-start gap-6 py-6 sm:py-8",
          aside ? "lg:grid-cols-[minmax(0,1fr)_20rem]" : "lg:grid-cols-[minmax(0,56rem)]",
        )}
      >
        <div className="bg-surface-paper border-border text-text [&_h2]:font-display flex min-w-0 flex-col gap-4 rounded-[24px] border p-5 leading-7 shadow-[0_1px_2px_rgb(40_30_10/0.06),0_8px_22px_rgb(40_30_10/0.07)] sm:p-8 [&_h2]:mt-4 [&_h2]:text-xl [&_h2]:font-extrabold [&_li]:ml-5 [&_li]:list-disc">
          {children}
        </div>
        {aside && <div className="flex min-w-0 flex-col gap-4">{aside}</div>}
      </Container>
    </>
  );
}
