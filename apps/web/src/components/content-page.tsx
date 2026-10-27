import { type IllustrationSlot, TextPageShell } from "@fresh/ui";
import { type ReactNode } from "react";
import { Breadcrumbs } from "./breadcrumbs";

/**
 * A text page (about, help, policies) on the shared `TextPageShell` from `@fresh/ui` (D181): the harbour-sky band
 * with the site's breadcrumbs, the heading (key word in a `Highlight`) and the intro, then the text on a paper card,
 * with `aside` beside it from `lg` up.
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
    <TextPageShell
      eyebrow={<Breadcrumbs tone="band" items={breadcrumbs} />}
      title={title}
      description={intro}
      picture={picture}
      side={aside}
    >
      {children}
    </TextPageShell>
  );
}
