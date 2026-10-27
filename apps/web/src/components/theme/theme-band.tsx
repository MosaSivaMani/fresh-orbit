import { AvatarCircle, Brush, type IllustrationSlot } from "@fresh/ui";
import { type ComponentProps, type ReactNode } from "react";
import { cx } from "@/lib/cx";
import { BandWaves, Gulls, Sparks, Swoosh } from "./marks";

/**
 * The key word of a heading on the yellow brush, as on the app and the home ("Cleaned your way.", "today?"). The
 * word stays real ink text, so it reads in every theme.
 */
export function Highlight({ className, ...props }: ComponentProps<"span">) {
  return <Brush className={cx("[--fo-brush-colour:#F5D04A]", className)} {...props} />;
}

/**
 * The harbour sky behind a whole-page state (not found, error): gulls at the top, waves at the foot, the content
 * centred between them.
 */
export function SkyPanel({ className, children, ...props }: ComponentProps<"div">) {
  return (
    <div className={cx("fo-sky relative isolate overflow-hidden pb-10", className)} {...props}>
      <Gulls className="text-outline-ink/40 absolute top-6 right-[8%] w-24 sm:w-32" />
      <Gulls className="text-outline-ink/30 absolute top-16 left-[6%] hidden w-20 sm:block" />
      <div className="relative mx-auto w-full max-w-3xl px-4">{children}</div>
      <BandWaves />
    </div>
  );
}

export interface ThemeBandProps extends Omit<ComponentProps<"section">, "title"> {
  /** Small line or breadcrumbs above the title. */
  eyebrow?: ReactNode;
  /** The page heading; put the key word in a `Highlight`. Coral sparks follow it. */
  title: ReactNode;
  /** One handwritten coral line under the title, with a swoosh (Dancing Script). */
  script?: ReactNode;
  description?: ReactNode;
  /** Right-side content (a status, a button). */
  aside?: ReactNode;
  /** An owner picture in a ringed circle at the right, from `sm` up. */
  picture?: { slot: IllustrationSlot; focus?: string };
  headingLevel?: 1 | 2;
}

/**
 * The top of every inner page (D181, D188): the home's warm harbour sky in a shorter band, a Baloo 2 heading with the
 * key word on the yellow brush and coral sparks, an optional handwritten line, a few gulls, an optional owner picture
 * in a circle, and waves curving into the page. `children` is a full-width row under the title (the search box).
 * Same component at every width; phones leave out the picture.
 */
export function ThemeBand({
  eyebrow,
  title,
  script,
  description,
  aside,
  picture,
  headingLevel = 1,
  className,
  children,
  ...props
}: ThemeBandProps) {
  const Heading = headingLevel === 1 ? "h1" : "h2";
  return (
    <section className={cx("fo-sky relative isolate overflow-hidden", className)} {...props}>
      <Gulls className="text-outline-ink/40 absolute top-4 right-[6%] w-20 sm:top-6 sm:w-28" />
      <div className="relative mx-auto flex max-w-7xl flex-wrap items-end justify-between gap-x-6 gap-y-4 px-4 pt-6 pb-14 sm:pt-8 sm:pb-20 lg:px-8">
        <div className="grid min-w-0 flex-1 gap-2">
          {eyebrow && <div className="text-text-muted text-sm font-semibold">{eyebrow}</div>}
          <Heading className="font-display text-text text-[2rem] leading-[1.1] font-extrabold tracking-[-0.01em] text-balance sm:text-[2.75rem]">
            {title}
            <Sparks className="text-accent-coral ml-1 w-7 -translate-y-4 sm:w-9" />
          </Heading>
          {script && (
            <p className="font-script text-accent-coral-text w-fit -rotate-2 text-xl leading-tight font-bold sm:text-2xl">
              {script}
              <Swoosh className="text-accent-coral mt-0.5 h-2 w-full" />
            </p>
          )}
          {description && (
            <p className="text-text-muted max-w-[60ch] text-sm font-medium sm:text-base">
              {description}
            </p>
          )}
        </div>
        {aside}
        {picture && (
          <AvatarCircle
            slot={picture.slot}
            focus={picture.focus}
            size="xs"
            className="hidden sm:block sm:size-32 lg:size-36"
          />
        )}
        {children && <div className="basis-full">{children}</div>}
      </div>
      <BandWaves />
    </section>
  );
}
