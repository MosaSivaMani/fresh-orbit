// Illustrated components (D53): slot illustrations, feature cards, polaroid cards with stickers, the help
// hero and the receipt. Pictures never carry text; every label here is real text.
import { type IllustrationEntry, type IllustrationSlot, illustrations } from "@fresh/illustrations";
import { type VariantProps, cva } from "class-variance-authority";
import {
  type CSSProperties,
  type ComponentProps,
  type ReactNode,
  createContext,
  createElement,
  useContext,
  useId,
} from "react";
import { cn } from "../lib/cn";

export type { IllustrationSlot } from "@fresh/illustrations";

type HeadingLevel = 1 | 2 | 3 | 4;
const heading = (level: HeadingLevel) => `h${level}` as const;

// ---------- Illustration ----------
const IllustrationContext = createContext<{ baseUrl: string }>({ baseUrl: "/illustrations" });

export interface IllustrationProviderProps {
  /** Folder the illustration files are served from (no trailing slash). Default "/illustrations". */
  baseUrl: string;
  children: ReactNode;
}

/** Sets where `<Illustration>` loads files from, for apps that serve them somewhere other than /illustrations. */
export function IllustrationProvider({ baseUrl, children }: IllustrationProviderProps) {
  return (
    <IllustrationContext.Provider value={{ baseUrl: baseUrl.replace(/\/+$/, "") }}>
      {children}
    </IllustrationContext.Provider>
  );
}

/**
 * True when the slot shows a full picture with its own scenery (a FINAL image whose background is SCENE).
 * Scenes are cropped with object-fit: cover inside a rounded frame; cut-outs and placeholders are shown whole.
 */
export function isSceneIllustration(
  slot: IllustrationSlot,
  manifest: Readonly<Record<string, IllustrationEntry>> = illustrations,
): boolean {
  return manifest[slot]?.background === "SCENE";
}

/** Rendered widths in CSS pixels. `fill` takes the container's width. */
export const illustrationSizes = { xs: 96, sm: 160, md: 240, lg: 320, xl: 480 } as const;
export type IllustrationSize = keyof typeof illustrationSizes | "fill";

/**
 * Resolves a slot to the file, alt text and intrinsic size to render: the final image (WebP or PNG) when
 * the manifest says FINAL, otherwise our placeholder SVG.
 */
export function resolveIllustration(
  slot: IllustrationSlot,
  baseUrl: string,
  size: IllustrationSize = "md",
  manifest: Readonly<Record<string, IllustrationEntry>> = illustrations,
): { src: string; alt: string; width: number; height: number; entry: IllustrationEntry } {
  const entry = manifest[slot];
  if (!entry) throw new Error(`Unknown illustration slot "${slot}"; add it to manifest.json.`);
  const width = size === "fill" ? entry.width : illustrationSizes[size];
  const height = Math.round((width * entry.height) / entry.width);
  return { src: `${baseUrl}/${entry.src}`, alt: entry.alt, width, height, entry };
}

export interface IllustrationProps extends Omit<
  ComponentProps<"img">,
  "src" | "alt" | "width" | "height" | "loading" | "children"
> {
  slot: IllustrationSlot;
  /** Rendered width; height follows the slot's aspect. Default `md` (240 px). `fill` = container width. */
  size?: IllustrationSize;
  /** Above-the-fold image (hero, onboarding): loads eagerly with high fetch priority. Default lazy. */
  priority?: boolean;
  /** The picture only repeats nearby text: `alt=""` so screen readers skip it. */
  decorative?: boolean;
  /** Subtle up-and-down float for hero pictures; none under reduced motion. Ignored for scenes. */
  float?: boolean;
  /**
   * How the picture fills its box. Default `auto`: a scene (full background) is cropped to the slot's shape
   * with object-fit: cover in a rounded frame; anything else is shown whole (contain).
   */
  fit?: "auto" | "cover" | "contain";
}

const sceneRadius: Record<IllustrationSize, string> = {
  xs: "rounded-xl",
  sm: "rounded-2xl",
  md: "rounded-3xl",
  lg: "rounded-[28px]",
  xl: "rounded-[28px]",
  fill: "rounded-[28px]",
};

/** One slot's picture, with alt text, width and height from `@fresh/illustrations`. */
export function Illustration({
  slot,
  size = "md",
  priority = false,
  decorative = false,
  float = false,
  fit = "auto",
  className,
  ...props
}: IllustrationProps) {
  const { baseUrl } = useContext(IllustrationContext);
  const { src, alt, width, height, entry } = resolveIllustration(slot, baseUrl, size);
  const cover = fit === "cover" || (fit === "auto" && entry.background === "SCENE");
  return (
    <img
      src={src}
      alt={decorative ? "" : alt}
      width={width}
      height={height}
      loading={priority ? "eager" : "lazy"}
      decoding={priority ? "auto" : "async"}
      fetchPriority={priority ? "high" : "auto"}
      draggable={false}
      data-slot={slot}
      data-status={entry.status}
      data-background={entry.background}
      className={cn(
        "h-auto max-w-full select-none",
        size === "fill" && "w-full",
        cover
          ? cn(
              "object-cover",
              entry.aspect === "1:1" ? "aspect-square" : "aspect-[4/3]",
              fit === "auto" && sceneRadius[size],
            )
          : "object-contain",
        float && !cover && "motion-safe:animate-fo-float",
        className,
      )}
      {...props}
    />
  );
}

// ---------- Sticker ----------
export const stickerVariants = cva(
  [
    "border-outline-ink font-display inline-flex items-center gap-1 rounded-full border-[2.5px] font-extrabold whitespace-nowrap",
    "[&_svg]:shrink-0",
  ],
  {
    variants: {
      tone: {
        coral: "bg-accent-coral text-on-accent-coral",
        mango: "bg-mango text-on-mango",
        teal: "bg-accent text-on-accent",
        paper: "bg-surface-paper text-text",
      },
      size: {
        sm: "shadow-sticker-sm px-3 py-1 text-xs leading-4 [&_svg]:size-3.5",
        md: "shadow-sticker px-4 py-2 text-[0.9375rem] leading-[1.1] [&_svg]:size-4",
      },
      tilt: { left: "-rotate-3", right: "rotate-3", none: "" },
    },
    defaultVariants: { tone: "coral", size: "md", tilt: "left" },
  },
);

export type StickerTone = NonNullable<VariantProps<typeof stickerVariants>["tone"]>;

export interface StickerProps
  extends ComponentProps<"span">, VariantProps<typeof stickerVariants> {}

/**
 * A pill label in Baloo 2 with an ink outline and an ink offset shadow, slightly rotated. Real text; never
 * put it inside an image.
 */
export function Sticker({ className, tone, size, tilt, ...props }: StickerProps) {
  return <span className={cn(stickerVariants({ tone, size, tilt }), className)} {...props} />;
}

// ---------- FeatureCard ----------
const artTones = {
  sand: "bg-surface-sand-strong",
  teal: "bg-teal-soft",
  ice: "bg-ice",
  coral: "bg-accent-coral-subtle",
} as const;

export interface FeatureCardProps extends Omit<ComponentProps<"article">, "title"> {
  slot: IllustrationSlot;
  title: ReactNode;
  description?: ReactNode;
  /** Background behind the picture. Default `sand`. */
  tone?: keyof typeof artTones;
  /** Default 3. */
  headingLevel?: HeadingLevel;
  /** Default true: the title already says what the picture shows. */
  illustrationDecorative?: boolean;
}

/** A big picture on top, a chunky headline and one plain line. No numbers: these are benefits, not steps. */
export function FeatureCard({
  slot,
  title,
  description,
  tone = "sand",
  headingLevel = 3,
  illustrationDecorative = true,
  className,
  ...props
}: FeatureCardProps) {
  return (
    <article
      className={cn(
        "bg-surface-paper text-text shadow-soft-md flex min-w-0 flex-col overflow-hidden rounded-[28px]",
        className,
      )}
      {...props}
    >
      <div className={cn("flex aspect-[4/3] items-center justify-center", artTones[tone])}>
        <Illustration
          slot={slot}
          size="fill"
          fit={isSceneIllustration(slot) ? "cover" : "contain"}
          decorative={illustrationDecorative}
          className="size-full"
        />
      </div>
      <div className="flex flex-col gap-1.5 px-[22px] pt-5 pb-6">
        {createElement(
          heading(headingLevel),
          { className: "font-display text-2xl leading-[1.05] font-bold text-balance" },
          title,
        )}
        {description && <p className="text-text-muted text-[0.9375rem]">{description}</p>}
      </div>
    </article>
  );
}

// ---------- PolaroidCard ----------
export type StickerCorner = "top-left" | "top-right" | "bottom-left" | "bottom-right";

export interface PolaroidSticker {
  label: ReactNode;
  tone?: StickerTone;
  corner: StickerCorner;
}

const cornerClasses: Record<StickerCorner, string> = {
  "top-left": "top-2 left-2 md:-top-4 md:-left-[18px]",
  "top-right": "top-2 right-2 md:top-6 md:-right-2.5",
  "bottom-left": "bottom-16 left-2 md:-bottom-3.5 md:-left-3.5",
  "bottom-right": "bottom-16 right-2 md:-bottom-3.5 md:-right-3.5",
};
const cornerTilt: Record<StickerCorner, "left" | "right"> = {
  "top-left": "left",
  "top-right": "right",
  "bottom-left": "right",
  "bottom-right": "left",
};

export interface PolaroidCardProps extends Omit<ComponentProps<"figure">, "children"> {
  slot: IllustrationSlot;
  /** Shown under the picture, as the figure caption. */
  caption: ReactNode;
  stickers?: readonly PolaroidSticker[];
  /** Tilt −2.5° on wide screens (never on narrow screens or with reduced motion). Default true. */
  tilt?: boolean;
  priority?: boolean;
  /** Default false: the polaroid's picture is content, so it keeps the manifest's alt text. */
  illustrationDecorative?: boolean;
}

/** A photo card with a thick ink border, a sand block shadow and sticker labels at its corners. */
export function PolaroidCard({
  slot,
  caption,
  stickers = [],
  tilt = true,
  priority = false,
  illustrationDecorative = false,
  className,
  ...props
}: PolaroidCardProps) {
  const captionId = useId();
  return (
    <figure
      aria-labelledby={captionId}
      className={cn(
        "border-outline-ink bg-surface-paper text-text shadow-polaroid relative m-0 w-full max-w-[440px] rounded-lg border-[3px] px-[22px] pt-[22px] pb-[18px]",
        tilt && "md:motion-safe:-rotate-[2.5deg]",
        className,
      )}
      {...props}
    >
      <div className="border-outline-ink bg-ice overflow-hidden border-2">
        <Illustration
          slot={slot}
          size="fill"
          fit={isSceneIllustration(slot) ? "cover" : "contain"}
          priority={priority}
          decorative={illustrationDecorative}
          className="block"
        />
      </div>
      <figcaption
        id={captionId}
        className="font-display text-accent-text pt-3.5 text-center text-[1.375rem] leading-tight font-bold"
      >
        {caption}
      </figcaption>
      {stickers.map((s, i) => (
        <Sticker
          key={`${s.corner}-${i}`}
          tone={s.tone ?? "coral"}
          tilt={cornerTilt[s.corner]}
          className={cn("absolute", cornerClasses[s.corner])}
        >
          {s.label}
        </Sticker>
      ))}
    </figure>
  );
}

// ---------- HelpHero ----------
const circleTones = {
  mango: "bg-mango",
  teal: "bg-teal-soft",
  coral: "bg-accent-coral-subtle",
  sand: "bg-surface-sand-strong",
} as const;

export interface HelpHeroProps extends Omit<ComponentProps<"section">, "title"> {
  /** For example "How can we help you today?". */
  title: ReactNode;
  description?: ReactNode;
  /** Buttons, links or a search field (call, chat, raise a complaint). */
  actions?: ReactNode;
  /** Default HELP_SUPPORT. */
  slot?: IllustrationSlot;
  /** Colour of the circle behind the picture. Default mango. */
  circleTone?: keyof typeof circleTones;
  /** Default 1: this is usually the help page's main heading. */
  headingLevel?: HeadingLevel;
  /** Default true: the heading says what the picture shows. */
  illustrationDecorative?: boolean;
}

/** Help page header: a big friendly question next to the support agent on a coloured circle. */
export function HelpHero({
  title,
  description,
  actions,
  slot = "HELP_SUPPORT",
  circleTone = "mango",
  headingLevel = 1,
  illustrationDecorative = true,
  className,
  ...props
}: HelpHeroProps) {
  const titleId = useId();
  return (
    <section
      aria-labelledby={titleId}
      className={cn("text-text flex flex-col gap-4", className)}
      {...props}
    >
      <div className="grid grid-cols-[1fr_auto] items-center gap-3 sm:gap-6">
        <div className="flex min-w-0 flex-col gap-1.5">
          {createElement(
            heading(headingLevel),
            {
              id: titleId,
              className:
                "font-display text-[1.75rem] leading-[1.02] font-extrabold text-balance sm:text-5xl",
            },
            title,
          )}
          {description && <p className="text-text-muted text-sm sm:text-lg">{description}</p>}
        </div>
        <div
          className={cn(
            "flex size-28 shrink-0 items-end justify-center overflow-hidden rounded-full sm:size-48",
            circleTones[circleTone],
          )}
        >
          <Illustration
            slot={slot}
            size="fill"
            fit={isSceneIllustration(slot) ? "cover" : "contain"}
            decorative={illustrationDecorative}
            className="size-full"
          />
        </div>
      </div>
      {actions && <div className="flex flex-wrap gap-3">{actions}</div>}
    </section>
  );
}

// ---------- HeroBanner ----------
export interface HeroBannerProps extends Omit<ComponentProps<"section">, "title"> {
  title: ReactNode;
  description?: ReactNode;
  /** Buttons or links (shop now, choose location). */
  actions?: ReactNode;
  /** Default HOME_HERO. Works for the onboarding slots too. */
  slot?: IllustrationSlot;
  /** Labels on the picture's corners. */
  stickers?: readonly PolaroidSticker[];
  /** Picture on the left on wide screens. Default false (right). */
  reverse?: boolean;
  /** Default 1. */
  headingLevel?: HeadingLevel;
  /** Default false: the hero picture is content. */
  illustrationDecorative?: boolean;
}

/**
 * Home or onboarding hero: headline and actions next to the picture. A scene image is cropped (cover) in a
 * big rounded frame; a cut-out or placeholder sits on a sand panel and floats. Text never sits on the
 * picture, so its contrast never depends on the image.
 */
export function HeroBanner({
  title,
  description,
  actions,
  slot = "HOME_HERO",
  stickers = [],
  reverse = false,
  headingLevel = 1,
  illustrationDecorative = false,
  className,
  ...props
}: HeroBannerProps) {
  const titleId = useId();
  const scene = isSceneIllustration(slot);
  return (
    <section
      aria-labelledby={titleId}
      className={cn("text-text grid items-center gap-6 md:grid-cols-2 md:gap-10", className)}
      {...props}
    >
      <div className={cn("flex min-w-0 flex-col gap-4", reverse && "md:order-2")}>
        {createElement(
          heading(headingLevel),
          {
            id: titleId,
            className:
              "font-display text-4xl leading-[1.02] font-extrabold text-balance sm:text-5xl lg:text-6xl",
          },
          title,
        )}
        {description && (
          <p className="text-text-muted max-w-prose text-base sm:text-lg">{description}</p>
        )}
        {actions && <div className="flex flex-wrap gap-3">{actions}</div>}
      </div>
      <div className="relative">
        <div
          className={cn(
            "flex aspect-[4/3] items-center justify-center overflow-hidden rounded-[32px]",
            scene ? "shadow-soft-lg" : "bg-surface-sand-strong",
          )}
        >
          <Illustration
            slot={slot}
            size="fill"
            fit={scene ? "cover" : "contain"}
            priority
            float={!scene}
            decorative={illustrationDecorative}
            className="size-full"
          />
        </div>
        {stickers.map((s, i) => (
          <Sticker
            key={`${s.corner}-${i}`}
            tone={s.tone ?? "coral"}
            tilt={cornerTilt[s.corner]}
            className={cn("absolute", cornerClasses[s.corner])}
          >
            {s.label}
          </Sticker>
        ))}
      </div>
    </section>
  );
}

// ---------- ReceiptCard ----------
export interface ReceiptLine {
  /** Stable key; defaults to the position. */
  id?: string;
  label: ReactNode;
  /** Already formatted server value, e.g. formatRupees(line.totalPaise). Shown as given. */
  value: ReactNode;
  /** Small text under the label (weight, preparation). */
  description?: ReactNode;
  /** `saving` for discounts, `muted` for notes such as "Calculated at delivery". */
  tone?: "default" | "muted" | "saving";
}

export interface ReceiptCardProps extends Omit<ComponentProps<"section">, "title"> {
  title: ReactNode;
  /** Store, packing time, order number: one short line under the title. */
  meta?: ReactNode;
  /** Groups of lines; a dashed divider separates groups. */
  sections: readonly (readonly ReceiptLine[])[];
  /** The total exactly as the server sent it, shown in a teal band. Never add the lines up in the client. */
  total?: ReceiptLine;
  /** The weight note from the server ("Weighed 12 g over, well under the 10% cap"), in a coral band. */
  note?: ReactNode;
  footer?: ReactNode;
  /** Default 2. */
  headingLevel?: HeadingLevel;
}

/** Scalloped bottom edge: 18 px bites, 7 px deep, cut with a CSS mask. */
const scallop =
  "radial-gradient(10px 7px at 50% 100%, #0000 98%, #000) 50% 100% / 18px 100% repeat-x";
const receiptMask: CSSProperties = { mask: scallop, WebkitMask: scallop };

const toneClass = {
  default: "",
  muted: "text-text-muted",
  saving: "text-success-text",
} as const;

function ReceiptRow({ line, className }: { line: ReceiptLine; className?: string }) {
  return (
    <div className={cn("flex items-baseline justify-between gap-2 tabular-nums", className)}>
      <dt className={cn("min-w-0", toneClass[line.tone ?? "default"])}>
        {line.label}
        {line.description && (
          <span className="text-text-muted block text-xs">{line.description}</span>
        )}
      </dt>
      <dd className={cn("shrink-0 text-right tabular-nums", toneClass[line.tone ?? "default"])}>
        {line.value}
      </dd>
    </div>
  );
}

/**
 * An order summary drawn as a paper receipt with a scalloped, torn-looking bottom. It renders only the
 * values passed in (CLAUDE.md rule 4): it never adds, rounds or recalculates anything.
 */
export function ReceiptCard({
  title,
  meta,
  sections,
  total,
  note,
  footer,
  headingLevel = 2,
  className,
  ...props
}: ReceiptCardProps) {
  const titleId = useId();
  return (
    <section
      aria-labelledby={titleId}
      className={cn(
        "text-text drop-shadow-[0_6px_14px_rgb(60_40_10_/_0.12)] dark:drop-shadow-[0_8px_18px_rgb(0_0_0_/_0.45)]",
        className,
      )}
      {...props}
    >
      <div
        style={receiptMask}
        className="bg-surface-paper flex flex-col gap-2.5 px-[18px] pt-5 pb-[30px] text-[0.84375rem]"
      >
        <div className="flex flex-col items-center gap-0.5 text-center">
          {createElement(
            heading(headingLevel),
            { id: titleId, className: "font-display text-xl leading-7 font-extrabold" },
            title,
          )}
          {meta && <p className="text-text-muted text-xs">{meta}</p>}
        </div>
        {sections.map((lines, s) => (
          <dl
            key={s}
            className="border-border flex flex-col gap-2.5 border-t-2 border-dashed pt-2.5"
          >
            {lines.map((line, i) => (
              <ReceiptRow key={line.id ?? i} line={line} />
            ))}
          </dl>
        ))}
        {total && (
          <dl className="border-border border-t-2 border-dashed pt-2.5">
            <ReceiptRow
              line={total}
              className="bg-teal-soft text-accent-text font-display rounded-xl px-3 py-2.5 text-[1.0625rem] font-extrabold"
            />
          </dl>
        )}
        {note && (
          <p className="bg-accent-coral-subtle text-accent-coral-text rounded-xl px-3 py-2 text-xs font-semibold">
            {note}
          </p>
        )}
        {footer && <div className="mt-1">{footer}</div>}
      </div>
    </section>
  );
}
