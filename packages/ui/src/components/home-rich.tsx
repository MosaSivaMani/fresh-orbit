// Rich home parts after the owner's website reference (D188), in the app's look (D176, D181): drawn harbour scenery
// (trawler, sky, waves; the sparks, swoosh and gulls come from the theme marks), the harbour hero with its headline
// and feature chips, the white check bar, pill links, round category tiles and promo cards.
//
// Our own shapes, written for the web after the app's scenery (not copied). Drawings are decorative (aria-hidden).
// Nothing here computes a business value (CLAUDE.md rule 4): labels, pictures, links and slot lines are passed in.
import { Slot } from "radix-ui";
import { Children, type ComponentProps, type ReactNode, createElement } from "react";
import { cn, focusRing } from "../lib/cn";
import { Brush, Script } from "./heading";
import { Gulls, Sparks, Swoosh } from "./theme";

type ArtProps = { className?: string };

// ---------- Scenery ----------

/** An orange-teal fishing trawler riding a wave: hull, cabin with windows, wheelhouse, mast, flag and rigging. */
export function Trawler({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 240 200"
      className={cn("block", className)}
    >
      {/* rigging and mast */}
      <g stroke="#14302F" strokeWidth={2.4} strokeLinecap="round" fill="none">
        <path d="M120 18V118" strokeWidth={4} />
        <path d="M120 26 46 112M120 26 206 110M120 52 78 112" />
        <path d="M120 64H170" strokeWidth={3} />
      </g>
      <path d="M122 18h26l-6 8 6 8h-26z" fill="#F07A5A" />
      {/* cabin and wheelhouse */}
      <rect
        x="84"
        y="88"
        width="78"
        height="38"
        rx="5"
        fill="#FFFDF8"
        stroke="#14302F"
        strokeWidth={3}
      />
      <rect
        x="104"
        y="66"
        width="40"
        height="26"
        rx="4"
        fill="#0B7D70"
        stroke="#14302F"
        strokeWidth={3}
      />
      <g fill="#BFE3EE" stroke="#14302F" strokeWidth={2}>
        <rect x="92" y="98" width="13" height="11" rx="2" />
        <rect x="112" y="98" width="13" height="11" rx="2" />
        <rect x="132" y="98" width="13" height="11" rx="2" />
        <rect x="113" y="73" width="22" height="9" rx="2" />
      </g>
      {/* hull */}
      <path
        d="M24 122h196c-4 20-16 36-34 42H56c-16-6-28-22-32-42z"
        fill="#F07A5A"
        stroke="#14302F"
        strokeWidth={3.2}
        strokeLinejoin="round"
      />
      <path d="M30 136h184" stroke="#0B7D70" strokeWidth={6} />
      <circle cx="70" cy="150" r="4" fill="#FFFDF8" stroke="#14302F" strokeWidth={2} />
      <circle cx="96" cy="150" r="4" fill="#FFFDF8" stroke="#14302F" strokeWidth={2} />
      {/* the wave under it */}
      <path
        d="M0 168c20-10 40-10 60 0s40 10 60 0 40-10 60 0 40 10 60 0v32H0z"
        fill="#0B7D70"
        opacity={0.85}
      />
      <path
        d="M0 178c20-8 40-8 60 0s40 8 60 0 40-8 60 0 40 8 60 0"
        fill="none"
        stroke="#FFFDF8"
        strokeWidth={3}
        strokeLinecap="round"
      />
    </svg>
  );
}

/**
 * The sunset harbour behind a hero: the sun with a glow, a far shore, soft water bands. The sky colour comes from the
 * hero's own gradient. Scales to cover its box (keeps the bottom edge).
 */
export function HarbourSky({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 460"
      preserveAspectRatio="xMidYMax slice"
      className={cn("absolute inset-0 size-full", className)}
    >
      <defs>
        <radialGradient id="fo-sun-glow" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#FFF1C4" stopOpacity="0.95" />
          <stop offset="0.45" stopColor="#FFD9A0" stopOpacity="0.55" />
          <stop offset="1" stopColor="#FFD9A0" stopOpacity="0" />
        </radialGradient>
        <linearGradient id="fo-water" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#CFE8E4" />
          <stop offset="1" stopColor="#A9D8D1" />
        </linearGradient>
      </defs>
      <circle cx="1290" cy="300" r="230" fill="url(#fo-sun-glow)" />
      <circle cx="1290" cy="300" r="74" fill="#FFE6A6" opacity={0.9} />
      {/* far shore and hills */}
      <path
        d="M980 330c60-34 120-46 180-40s110 12 160-14 120-60 200-38 80 30 80 30v62H980z"
        fill="#7FB7B0"
        opacity={0.45}
      />
      <path d="M0 336c80-14 160-18 260-10s160 4 220-6v40H0z" fill="#E7B48F" opacity={0.35} />
      {/* soft water */}
      <path
        d="M0 340c260-14 520-16 800-8s560 10 800-6v134H0z"
        fill="url(#fo-water)"
        opacity={0.75}
      />
      <g fill="none" stroke="#FFFDF8" strokeWidth={3} strokeLinecap="round" opacity={0.7}>
        <path d="M640 368c30-8 60-8 90 0M900 382c30-8 60-8 90 0M1120 360c24-6 48-6 72 0" />
      </g>
    </svg>
  );
}

/**
 * The waves at the bottom of a hero: a teal band and a pale band, then the page colour curving up into the hero.
 * Stretches to the full width.
 */
export function HeroWaves({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 120"
      preserveAspectRatio="none"
      className={cn(
        "pointer-events-none absolute inset-x-0 bottom-0 block h-[72px] w-full lg:h-[110px]",
        className,
      )}
    >
      <path
        d="M0 46c120-26 240-30 380-10s260 34 420 14 300-48 460-34 220 26 340 10v94H0z"
        fill="#9ED3CC"
        opacity={0.75}
      />
      <path
        d="M0 66c140-20 280-22 420-6s300 30 460 10 300-40 440-30 200 22 280 14v66H0z"
        fill="#D8EEEA"
      />
      <path
        className="fill-surface"
        d="M0 88c160-16 320-18 480-6s320 22 480 8 320-30 460-22 140 12 180 10v42H0z"
      />
    </svg>
  );
}

// ---------- HeroHeadline ----------
export interface HeroHeadlineProps extends Omit<ComponentProps<"h1">, "children"> {
  /** The big handwritten words with coral sparks ("Fresh fish."). */
  script: ReactNode;
  /** The handwritten line on the yellow brush ("Cleaned your way."). */
  brush: ReactNode;
  /** The bold Baloo line ("At your door, in your slot."). */
  line: ReactNode;
  /** h1 by default. */
  level?: 1 | 2;
}

/** The three-line hero headline: a big script line with sparks, a script line on the yellow brush, a bold line. */
export function HeroHeadline({
  script,
  brush,
  line,
  level = 1,
  className,
  ...props
}: HeroHeadlineProps) {
  return createElement(
    `h${level}`,
    {
      className: cn("text-text flex flex-col items-center lg:items-start", className),
      ...props,
    },
    <span key="script" className="inline-flex items-start text-[clamp(2.875rem,5.6vw,4.75rem)]">
      <Script className="leading-[0.95]">{script}</Script>
      <Sparks className="text-accent-coral -mt-1 ml-1 w-9 lg:w-11" />
    </span>,
    " ",
    <Brush
      key="brush"
      className="-mt-1 text-[clamp(1.75rem,3.2vw,2.625rem)] [--fo-brush-colour:#F5D04A]"
    >
      <Script>{brush}</Script>
    </Brush>,
    " ",
    <span
      key="line"
      className="font-display mt-3 text-[clamp(1.75rem,3.2vw,2.625rem)] leading-[1.05] font-extrabold text-balance"
    >
      {line}
    </span>,
  );
}

// ---------- FeatureChip ----------
export interface FeatureChipProps extends Omit<ComponentProps<"li">, "title"> {
  /** Icon in the teal-tint circle (decorative). */
  icon: ReactNode;
  /** Caps word ("CLEANED"). */
  title: ReactNode;
  /** Second line ("Your way"). */
  line: ReactNode;
  /** Small muted line ("Whole · Steaks · Curry cut", or the server's next slot). */
  detail?: ReactNode;
}

/** A hero feature: a teal-tint circle icon and three lines. A list item; put it in a `ul`. */
export function FeatureChip({ icon, title, line, detail, className, ...props }: FeatureChipProps) {
  return (
    <li className={cn("flex min-w-[10.5rem] items-center gap-3 text-left", className)} {...props}>
      <span
        aria-hidden="true"
        className="bg-teal-soft text-accent-text ring-surface/80 grid size-12 shrink-0 place-items-center rounded-full ring-4 [&_svg]:size-6"
      >
        {icon}
      </span>
      <span className="flex flex-col leading-tight">
        <span className="font-display text-text text-[0.9375rem] font-extrabold tracking-[0.02em]">
          {title}
        </span>
        <span className="text-text text-sm font-semibold">{line}</span>
        {detail && <span className="text-text-muted text-xs">{detail}</span>}
      </span>
    </li>
  );
}

// ---------- HarbourHero ----------
export interface HarbourHeroProps extends ComponentProps<"section"> {
  /** The headline, normally a `HeroHeadline` (give it an id and pass it as `aria-labelledby`). */
  heading: ReactNode;
  /** `FeatureChip`s. */
  features?: ReactNode;
  /** Accessible name of the feature list. */
  featuresLabel?: string;
  /**
   * The handwritten note at the top right (phones: under the chips), with a swash. Pass an array to set the line
   * breaks ("From", "today's catch", "to your kitchen").
   */
  tagline?: ReactNode | readonly string[];
  /** The character picture: the left 35 % on desktop, above the text on phones (an `<img>` filling its box). */
  picture?: ReactNode;
  /** An optional background picture covering the hero (decorative). Without it the harbour sky is drawn. */
  background?: ReactNode;
  /**
   * The owner's full harbour picture (D279), which already holds Padma, the sky, the gulls and the trawler: an `<img>`
   * with alt text that fills its box (`size-full object-cover`, with its own focus point). It replaces `picture`,
   * `background` and every drawing; only the text is laid over its calm middle. Phones show it above the text.
   */
  scene?: ReactNode;
}

/**
 * The full-bleed harbour hero (D188, D247): 500 px tall from `lg`, a peach-to-cream sky with the drawn sun and water
 * (or the background picture), gulls, a trawler at the bottom right, waves curving into the page. From `lg` the
 * picture fills the left 35 % at the page edge and the headline and feature chips start beside it, so text is never
 * under the picture; the handwritten note sits at the top right. Below `lg` the picture stacks above the text.
 *
 * With a `scene` (D279) the owner's picture covers the whole hero from `lg` (at least 440 px, 35 % of the width up to
 * 620 px): the text starts beside Padma and stops short of the trawler, on a soft cream glow so it stays readable;
 * the waves are lower so the picture shows. Below `lg` the picture is a 4:3 (2:1 from `sm`) band above the text.
 */
export function HarbourHero({
  heading,
  features,
  featuresLabel,
  tagline,
  picture,
  background,
  scene,
  className,
  ...props
}: HarbourHeroProps) {
  const lines = Array.isArray(tagline) ? (tagline as readonly string[]) : null;
  const note = lines
    ? lines.map((line) => createElement("span", { key: line, className: "block" }, line))
    : tagline;
  const hasScene = Boolean(scene);
  return (
    <section
      className={cn(
        "relative isolate overflow-hidden bg-[linear-gradient(180deg,#F8C6A0_0%,#FBDDBE_42%,#FFF3E4_100%)]",
        hasScene ? "lg:min-h-[clamp(440px,35vw,620px)]" : "lg:h-[500px]",
        className,
      )}
      {...props}
    >
      {hasScene ? (
        <div className="relative -z-10 aspect-[4/3] w-full sm:aspect-[2/1] lg:absolute lg:inset-0 lg:aspect-auto">
          {scene}
          <div
            aria-hidden="true"
            className="absolute inset-x-0 bottom-0 h-16 bg-linear-to-b from-transparent to-[#F9CFAE] lg:hidden"
          />
        </div>
      ) : (
        <>
          {background ? (
            <div
              aria-hidden="true"
              className="absolute inset-0 -z-10 [&_img]:size-full [&_img]:object-cover"
            >
              {background}
            </div>
          ) : (
            <HarbourSky className="-z-10" />
          )}

          <Gulls className="text-outline-ink/45 absolute top-6 left-[52%] hidden w-28 lg:block" />
          <Gulls className="text-outline-ink/40 absolute top-24 right-[4%] w-20 lg:w-24" />
          <Trawler className="absolute right-[1%] bottom-16 hidden w-[210px] lg:block" />
        </>
      )}

      <div
        className={cn(
          "relative flex h-full flex-col items-center px-4 pt-6 pb-20 lg:grid lg:grid-cols-[35%_minmax(0,1fr)] lg:items-stretch lg:px-0 lg:pt-0 lg:pb-0",
          hasScene && "lg:pb-32",
        )}
      >
        {hasScene ? (
          <div aria-hidden="true" className="hidden lg:block" />
        ) : (
          picture && (
            <div className="relative aspect-[5/4] w-full max-w-[440px] lg:aspect-auto lg:h-full lg:max-w-none">
              {picture}
            </div>
          )
        )}
        <div
          className={cn(
            "mt-4 flex flex-col items-center gap-6 text-center lg:mt-0 lg:items-start lg:pt-12 lg:pr-8 lg:pl-[4%] lg:text-left",
            hasScene &&
              "lg:bg-[radial-gradient(closest-side,rgb(255_247_236/0.82),rgb(255_247_236/0))] lg:pr-[22%]",
          )}
        >
          {heading}
          {features && (
            <ul
              aria-label={featuresLabel}
              className="flex flex-wrap justify-center gap-x-6 gap-y-4 lg:justify-start lg:gap-x-5"
            >
              {features}
            </ul>
          )}
        </div>

        {tagline && (
          <p
            className={cn(
              "text-accent-coral-text font-script mt-6 -rotate-[8deg] text-center text-[1.625rem] leading-[1.05] font-bold lg:absolute lg:top-10 lg:right-8 lg:mt-0 lg:text-left lg:text-[1.875rem] xl:text-[2.125rem]",
              hasScene &&
                "lg:bg-[radial-gradient(closest-side,rgb(255_247_236/0.85),rgb(255_247_236/0))] lg:p-4",
            )}
          >
            {note}
            <Swoosh className="text-accent-coral mt-1 h-3 w-40 lg:w-48" />
          </p>
        )}
      </div>

      <HeroWaves className={hasScene ? "lg:h-[72px]" : undefined} />
    </section>
  );
}

// ---------- CheckBar ----------
export interface CheckBarProps extends ComponentProps<"div"> {
  /** Left part (the pincode check). */
  start: ReactNode;
  /** Right part, after a divider (the search). */
  end: ReactNode;
}

/**
 * The white bar overlapping the bottom of a hero (D188): two parts with a divider, stacked on phones. The forms
 * inside get a thin border instead of their own shadow.
 */
export function CheckBar({ start, end, className, ...props }: CheckBarProps) {
  return (
    <div
      className={cn("relative z-10 mx-auto -mt-16 max-w-5xl px-4 lg:-mt-24 lg:px-8", className)}
      {...props}
    >
      <div className="bg-surface grid gap-3 rounded-3xl p-3 shadow-[0_12px_32px_rgb(10_38_48/0.14)] md:grid-cols-[minmax(0,1fr)_1px_minmax(0,1.2fr)] md:items-start md:gap-4 md:p-4">
        <div className="[&_form]:border-border [&_form]:border-[1.5px] [&_form]:shadow-none">
          {start}
        </div>
        <span aria-hidden="true" className="bg-border hidden self-stretch md:block" />
        <div>{end}</div>
      </div>
    </div>
  );
}

// ---------- PillLink ----------
const pillTones = {
  /** Teal outline on the page ("View all"). */
  outline:
    "border-primary text-primary hover:bg-primary hover:text-on-primary border-[1.5px] px-4 transition-colors",
  /** Dark teal fill. */
  dark: "bg-primary text-on-primary shadow-xs px-4",
  /** White with teal text. */
  light: "bg-surface text-primary shadow-xs px-4",
  /** White with coral text. */
  coral: "bg-surface text-accent-coral-text shadow-xs px-4",
} as const;
export type PillTone = keyof typeof pillTones;

export interface PillLinkProps extends ComponentProps<"a"> {
  tone?: PillTone;
  /** Render the single child (e.g. a Next.js Link) with these styles. */
  asChild?: boolean;
}

/** A rounded pill link; put an arrow icon after the text. */
export function PillLink({
  tone = "outline",
  asChild = false,
  className,
  ...props
}: PillLinkProps) {
  const Comp = asChild ? Slot.Root : "a";
  return (
    <Comp
      className={cn(
        "inline-flex shrink-0 items-center gap-1.5 self-start rounded-full py-2 text-sm font-bold [&_svg]:size-4",
        pillTones[tone],
        focusRing,
        className,
      )}
      {...props}
    />
  );
}

// ---------- CategoryTile ----------
export interface CategoryTileProps extends Omit<ComponentProps<"a">, "children"> {
  label: ReactNode;
  /** The picture (an `<img>`, decorative; the label names the category) or a fallback icon. */
  picture?: ReactNode;
  /** Render the single child (e.g. a Next.js Link) with these styles. */
  asChild?: boolean;
  children?: ReactNode;
}

/** A round category picture (96 px, 112 px from `xl`) on a peach disc, with the label under it. */
export function CategoryTile({
  label,
  picture,
  asChild = false,
  className,
  children,
  ...props
}: CategoryTileProps) {
  const Comp = asChild ? Slot.Root : "a";
  return (
    <Comp
      className={cn(
        "group flex flex-col items-center gap-2 rounded-2xl text-center",
        focusRing,
        className,
      )}
      {...props}
    >
      {asChild && <Slot.Slottable>{children}</Slot.Slottable>}
      <span
        aria-hidden="true"
        className="bg-accent-coral-subtle grid size-24 place-items-center overflow-hidden rounded-full transition-transform group-hover:scale-[1.03] motion-reduce:transition-none xl:size-[112px] [&_img]:size-full [&_img]:object-cover [&_svg]:size-10"
      >
        {picture}
      </span>
      <span className="text-text text-sm leading-tight font-semibold">{label}</span>
    </Comp>
  );
}

/** A row of `CategoryTile`s: one row, scrolling sideways inside the page gutters until it fits (from `xl`). */
export function CategoryTileRow({
  label,
  children,
  className,
}: {
  /** Name of the navigation landmark. */
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <nav aria-label={label} className={className}>
      <ul className="-mx-4 flex gap-4 overflow-x-auto px-4 pb-2 lg:-mx-8 lg:px-8 xl:mx-0 xl:justify-between xl:gap-2 xl:overflow-visible xl:px-0">
        {Children.map(children, (child) => (
          <li className="w-24 shrink-0 xl:w-[120px]">{child}</li>
        ))}
      </ul>
    </nav>
  );
}

// ---------- WaveBand ----------
/**
 * A full-width pale band whose top edge is a soft wave (D188: behind the promo cards). Put a page container inside.
 */
export function WaveBand({ className, children, ...props }: ComponentProps<"div">) {
  return (
    <div className={cn("relative isolate pt-10 pb-8 lg:pt-14", className)} {...props}>
      <svg
        aria-hidden="true"
        focusable="false"
        viewBox="0 0 1600 120"
        preserveAspectRatio="none"
        className="absolute inset-x-0 top-0 -z-10 block h-16 w-full lg:h-24"
      >
        <path d="M0 70c180-40 360-50 560-30s380 40 560 20 320-50 480-40v100H0z" fill="#E4F3F8" />
        <path
          d="M0 92c200-26 400-30 600-14s400 28 600 12 300-28 400-24v54H0z"
          fill="#D8EEEA"
          opacity={0.6}
        />
      </svg>
      <div
        aria-hidden="true"
        className="bg-ice-soft absolute inset-x-0 top-16 bottom-0 -z-10 lg:top-24"
      />
      {children}
    </div>
  );
}

// ---------- PromoCard ----------
const promoTones = {
  teal: "bg-teal-soft",
  coral: "bg-accent-coral-subtle",
  sky: "bg-ice-soft",
} as const;
export type PromoTone = keyof typeof promoTones;

export interface PromoCardProps extends Omit<ComponentProps<"div">, "title"> {
  title: ReactNode;
  line?: ReactNode;
  /** The picture on the left (an `<img>` filling its box, with alt text). */
  picture?: ReactNode;
  /** Normally a `PillLink`. */
  action?: ReactNode;
  tone?: PromoTone;
  headingLevel?: 2 | 3 | 4;
}

/** A tinted promo card: picture on the left 44 %, then the title, one line and a pill link. At least 160 px tall. */
export function PromoCard({
  title,
  line,
  picture,
  action,
  tone = "teal",
  headingLevel = 3,
  className,
  ...props
}: PromoCardProps) {
  return (
    <div
      className={cn(
        "flex h-full min-h-40 overflow-hidden rounded-2xl shadow-[0_6px_18px_rgb(10_38_48/0.08)]",
        promoTones[tone],
        className,
      )}
      {...props}
    >
      {picture && (
        <div className="relative w-[44%] shrink-0 [&_img]:object-contain [&_img]:p-2">
          {picture}
        </div>
      )}
      <div className="flex flex-1 flex-col justify-center gap-1 py-4 pr-4 pl-4 [div+&]:pl-0">
        {createElement(
          `h${headingLevel}`,
          { className: "font-display text-text text-xl leading-tight font-extrabold" },
          title,
        )}
        {line && <p className="text-text-muted text-sm">{line}</p>}
        {action && <div className="mt-2">{action}</div>}
      </div>
    </div>
  );
}

/** Promo cards in a grid: one column on phones, two from `sm`, three from `lg`. */
export function PromoCardGrid({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <ul className={cn("grid gap-4 sm:grid-cols-2 lg:grid-cols-3", className)}>
      {Children.map(children, (child) => (
        <li>{child}</li>
      ))}
    </ul>
  );
}
