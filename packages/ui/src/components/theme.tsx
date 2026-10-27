// The app theme for the website's pages (D181, D188): the highlight marks (sparks, swoosh, gulls, band waves), the
// yellow-brush highlight, the harbour-sky page band, the sky panel behind whole-page states, the text-page shell and
// the section header. Our own shapes, drawn after the app's look. Every mark is decorative: aria-hidden, no text, no
// focus, no motion. None of these compute business values (CLAUDE.md rule 4).
import { Slot } from "radix-ui";
import { type ComponentProps, type ElementType, type ReactNode, createElement } from "react";
import { cn, focusRing } from "../lib/cn";
import { AvatarCircle } from "./bold-blocks";
import { Brush, Heading, Script } from "./heading";
import { type IllustrationSlot } from "./illustrated";

type MarkProps = { className?: string };

// ---------- Marks ----------

/** Three short fanned dashes next to a highlighted word. Colour = currentColor (coral on the page). */
export function Sparks({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 34 30"
      className={cn("inline-block shrink-0", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={3.6}
      strokeLinecap="round"
    >
      <path d="M6 22 10 10M14 24 24 8M22 26 32 20" />
    </svg>
  );
}

/** A loose brush stroke under a handwritten line. Colour = currentColor. */
export function Swoosh({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 140 9"
      preserveAspectRatio="none"
      className={cn("block", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={2.2}
      strokeLinecap="round"
    >
      <path d="M2 7q60-8 136-3" />
    </svg>
  );
}

/** Four gulls, two soft arcs each. Colour = currentColor. */
export function Gulls({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 120 50"
      className={cn("block", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M4 34q6-7 12 0q6-7 12 0" />
      <path d="M40 18q5-6 10 0q5-6 10 0" />
      <path d="M78 30q4-5 8 0q4-5 8 0" />
      <path d="M96 8q4-5 8 0q4-5 8 0" />
    </svg>
  );
}

/**
 * The waves at the foot of a band: a soft teal band, a pale band, then the page colour curving up into the band.
 * Stretches to the full width of its positioned parent.
 */
export function BandWaves({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 80"
      preserveAspectRatio="none"
      className={cn(
        "pointer-events-none absolute inset-x-0 bottom-0 block h-10 w-full sm:h-14",
        className,
      )}
    >
      <path
        d="M0 30c140-18 300-22 460-8s300 22 460 6 320-30 460-22 160 12 220 8v66H0z"
        fill="#9ED3CC"
        opacity={0.6}
      />
      <path
        d="M0 46c160-14 320-16 480-4s320 18 480 6 300-24 440-18 160 10 200 8v42H0z"
        fill="#D8EEEA"
      />
      <path
        className="fill-surface"
        d="M0 62c180-12 340-14 500-4s320 14 480 4 300-18 440-12 140 8 180 6v24H0z"
      />
    </svg>
  );
}

// ---------- Highlight ----------

/**
 * The key word of a heading on the yellow brush, as on the app and the home ("Cleaned your way.", "today?"). The
 * word stays real ink text, so it reads in every theme.
 */
export function Highlight({ className, ...props }: ComponentProps<"span">) {
  return <Brush className={cn("[--fo-brush-colour:#F5D04A]", className)} {...props} />;
}

// ---------- ThemeBand ----------
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
 * The top of every inner page (D181, D188): the home's warm harbour sky (`.fo-sky`) in a shorter band, a Baloo 2
 * heading with the key word on the yellow brush and coral sparks, an optional handwritten line, gulls, an optional
 * owner picture in a circle, and waves curving into the page. `children` is a full-width row under the title (the
 * search box). Same component at every width; phones leave out the picture.
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
  return (
    <section className={cn("fo-sky relative isolate overflow-hidden", className)} {...props}>
      <Gulls className="text-outline-ink/40 absolute top-4 right-[6%] w-20 sm:top-6 sm:w-28" />
      <div className="relative mx-auto flex max-w-7xl flex-wrap items-end justify-between gap-x-6 gap-y-4 px-4 pt-6 pb-14 sm:pt-8 sm:pb-20 lg:px-8">
        <div className="grid min-w-0 flex-1 gap-2">
          {eyebrow && <div className="text-text-muted text-sm font-semibold">{eyebrow}</div>}
          {createElement(
            headingLevel === 1 ? "h1" : "h2",
            {
              className:
                "font-display text-text text-[2rem] leading-[1.1] font-extrabold tracking-[-0.01em] text-balance sm:text-[2.75rem]",
            },
            title,
            <Sparks key="sparks" className="text-accent-coral ml-1 w-7 -translate-y-4 sm:w-9" />,
          )}
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

// ---------- SkyPanel ----------

/**
 * The harbour sky behind a whole-page state (not found, error): gulls at the top, waves at the foot, the content
 * centred between them.
 */
export function SkyPanel({ className, children, ...props }: ComponentProps<"div">) {
  return (
    <div className={cn("fo-sky relative isolate overflow-hidden pb-10", className)} {...props}>
      <Gulls className="text-outline-ink/40 absolute top-6 right-[8%] w-24 sm:w-32" />
      <Gulls className="text-outline-ink/30 absolute top-16 left-[6%] hidden w-20 sm:block" />
      <div className="relative mx-auto w-full max-w-3xl px-4">{children}</div>
      <BandWaves />
    </div>
  );
}

// ---------- TextPageShell ----------
export interface TextPageShellProps extends Omit<ThemeBandProps, "children" | "aside"> {
  /** A column beside the text card from `lg` up (contact, other policies); under it on phones. */
  side?: ReactNode;
  children: ReactNode;
}

/**
 * A text page (about, help, policies): the `ThemeBand`, then the text on a paper card (radius 24) with `h2`s in the
 * display face and bulleted lists, and an optional side column.
 */
export function TextPageShell({ side, children, ...band }: TextPageShellProps) {
  return (
    <>
      <ThemeBand {...band} />
      <div
        className={cn(
          "mx-auto grid w-full max-w-7xl items-start gap-6 px-4 py-6 sm:py-8 lg:px-8",
          side ? "lg:grid-cols-[minmax(0,1fr)_20rem]" : "lg:grid-cols-[minmax(0,56rem)]",
        )}
      >
        <div className="bg-surface-paper border-border text-text [&_h2]:font-display flex min-w-0 flex-col gap-4 rounded-[24px] border p-5 leading-7 shadow-[0_1px_2px_rgb(40_30_10/0.06),0_8px_22px_rgb(40_30_10/0.07)] sm:p-8 [&_h2]:mt-4 [&_h2]:text-xl [&_h2]:font-extrabold [&_li]:ml-5 [&_li]:list-disc">
          {children}
        </div>
        {side && <div className="flex min-w-0 flex-col gap-4">{side}</div>}
      </div>
    </>
  );
}

// ---------- SectionHeader ----------
export interface SectionHeaderProps {
  /** The heading's id, for the section's `aria-labelledby`. */
  id: string;
  title: ReactNode;
  /** A handwritten teal word after the title (D89); coral sparks follow. */
  script?: ReactNode;
  description?: ReactNode;
  /** A link on the right, usually a `SectionAction`. */
  action?: ReactNode;
}

/**
 * The outlined dark-teal pill for a section's "View all →" link, as on the owner's reference. Wrap the app's link:
 * `<SectionAction asChild><Link href="/shop">View all <ArrowRight /></Link></SectionAction>`.
 */
export function SectionAction({
  asChild = false,
  className,
  ...props
}: ComponentProps<"a"> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "a";
  return (
    <Comp
      className={cn(
        "border-primary text-primary hover:bg-primary hover:text-on-primary inline-flex shrink-0 items-center gap-1 rounded-full border-[1.5px] px-4 py-1.5 text-sm font-bold [&_svg]:size-4",
        focusRing,
        className,
      )}
      {...props}
    />
  );
}

/** A section's title row: Baloo 2 heading, teal script word, coral sparks, an optional line and action. */
export function SectionHeader({ id, title, script, description, action }: SectionHeaderProps) {
  return (
    <div className="flex items-end justify-between gap-4">
      <div className="flex flex-col gap-1">
        <Heading id={id} level={2} size="section" className="text-text">
          {title}
          {script && (
            <>
              {" "}
              <Script tone="accent">{script}</Script>
            </>
          )}
          <Sparks className="text-accent-coral ml-1 w-6 -translate-y-3 sm:w-7" />
        </Heading>
        {description && <p className="text-text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}

// ---------- CompactProductCard ----------
export interface CompactProductCardProps {
  href: string;
  /** The app's link component (Next.js `Link` on the website); default a plain anchor. */
  linkAs?: ElementType;
  /** The picture element (the app's image component), shown in a 96 px rounded square. */
  picture: ReactNode;
  name: ReactNode;
  /** The server's price line, already formatted. */
  price?: ReactNode;
  /** A short coral note under the price ("Sold out for now"). */
  note?: ReactNode;
  className?: string;
}

/**
 * A small horizontal product card (saved items, short lists): picture, name, the server's price line and a note.
 * Radius 20 (D189). The whole card is one link.
 */
export function CompactProductCard({
  href,
  linkAs: LinkComp = "a",
  picture,
  name,
  price,
  note,
  className,
}: CompactProductCardProps) {
  return (
    <LinkComp
      href={href}
      className={cn(
        "bg-surface-paper border-border hover:border-border-strong flex gap-3 rounded-[20px] border p-3 shadow-[0_1px_2px_rgb(40_30_10/0.06),0_8px_22px_rgb(40_30_10/0.07)]",
        focusRing,
        className,
      )}
    >
      <span className="bg-ice-soft relative block size-24 shrink-0 overflow-hidden rounded-[16px]">
        {picture}
      </span>
      <span className="flex min-w-0 flex-col justify-center gap-1">
        <span className="font-display text-text text-lg leading-tight font-extrabold">{name}</span>
        {price && <span className="text-text text-sm">{price}</span>}
        {note && <span className="text-accent-coral-text text-xs font-bold">{note}</span>}
      </span>
    </LinkComp>
  );
}

// ---------- OfferRow ----------
export interface OfferRowProps {
  /** The coupon code. */
  code: string;
  /** The server's description. */
  description?: ReactNode;
  /** Server terms already formatted (minimum order, end date). */
  terms?: ReactNode;
  /** The Apply button (the app's `Button`). */
  action: ReactNode;
}

/** One coupon in a "View offers" list: code, description and terms, and the Apply action. */
export function OfferRow({ code, description, terms, action }: OfferRowProps) {
  return (
    <li className="bg-surface-paper border-border flex items-center justify-between gap-3 rounded-[16px] border p-3 text-sm">
      <div className="min-w-0">
        <p className="text-text font-bold tracking-[0.04em]">{code}</p>
        {description && <p className="text-text-muted">{description}</p>}
        {terms && <p className="text-text-muted text-xs">{terms}</p>}
      </div>
      {action}
    </li>
  );
}
