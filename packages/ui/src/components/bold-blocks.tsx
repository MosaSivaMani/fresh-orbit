// "Bold blocks" components (D88): one strong colour block per hero/empty screen with a paper sheet rising from
// the bottom, characters in big ringed circle avatars, stickers, a bento grid, category bubbles, product cards
// whose ADD button turns into a stepper, a floating cart bar and the food-app tab bar.
//
// None of these compute business values (CLAUDE.md rule 4): prices, totals, counts and labels are passed in
// already decided by the server; a stepper only proposes a new requested quantity.
import { type VariantProps, cva } from "class-variance-authority";
import { ChevronRight } from "lucide-react";
import { Slot } from "radix-ui";
import { type ComponentProps, type ReactNode, createElement, useId } from "react";
import { cn, focusRing } from "../lib/cn";
import { type IllustrationSlot, Illustration, Sticker, type StickerTone } from "./illustrated";
import { QuantityStepper } from "./commerce";
import { headingLines } from "./heading";
import { Spinner } from "./spinner";

type HeadingLevel = 1 | 2 | 3 | 4;
const heading = (level: HeadingLevel) => `h${level}` as const;

/** The four block colours of D88: coral welcome·cart·saved, teal home·tracking, mango orders·sign-in, ice location·alerts. */
export type BlockTone = "coral" | "teal" | "mango" | "ice";

const blockTones: Record<BlockTone, string> = {
  coral: "bg-block-coral text-on-block-coral",
  teal: "bg-block-teal text-on-block-teal",
  mango: "bg-block-mango text-on-block-mango",
  ice: "bg-block-ice text-on-block-ice",
};

// ---------- AvatarCircle ----------
export const avatarSizes = { xs: 96, sm: 150, md: 196, lg: 240 } as const;
export type AvatarSize = keyof typeof avatarSizes;

const avatarClasses: Record<AvatarSize, string> = {
  xs: "size-24 border-[3px] shadow-[0_0_0_2px_var(--fo-color-outline-ink)]",
  sm: "size-[150px] border-[5px] shadow-[0_0_0_3px_var(--fo-color-outline-ink),var(--fo-shadow-soft-lg)]",
  md: "size-[196px] border-[5px] shadow-[0_0_0_3px_var(--fo-color-outline-ink),var(--fo-shadow-soft-lg)]",
  lg: "size-60 border-[6px] shadow-[0_0_0_3px_var(--fo-color-outline-ink),var(--fo-shadow-soft-lg)]",
};

export interface AvatarCircleProps extends Omit<ComponentProps<"div">, "children"> {
  slot: IllustrationSlot;
  /** xs 96 (bento peek), sm 150, md 196 (default), lg 240. */
  size?: AvatarSize;
  /** Which part of the picture shows inside the circle, as CSS object-position. Default "50% 30%". */
  focus?: string;
  /** Default true: the screen's heading says what the picture shows. */
  decorative?: boolean;
  priority?: boolean;
}

/**
 * A character in a round frame: paper border, ink ring and a soft shadow. Never a pasted rectangle (D88). No
 * decorative motion: the halo and orbit dot were tried and removed by the owner.
 */
export function AvatarCircle({
  slot,
  size = "md",
  focus = "50% 30%",
  decorative = true,
  priority = false,
  className,
  ...props
}: AvatarCircleProps) {
  return (
    <div
      data-avatar-size={size}
      className={cn(
        "border-surface-paper bg-surface-paper relative shrink-0 overflow-hidden rounded-full",
        avatarClasses[size],
        className,
      )}
      {...props}
    >
      <Illustration
        slot={slot}
        size="fill"
        fit="cover"
        decorative={decorative}
        priority={priority}
        className="aspect-auto size-full rounded-none object-cover"
        style={{ objectPosition: focus }}
      />
    </div>
  );
}

// ---------- ColourBlockScreen ----------
export interface BlockSticker {
  label: ReactNode;
  tone?: StickerTone;
  /** Which side of the avatar the sticker sits on. */
  side: "left" | "right";
}

export interface ColourBlockScreenProps extends Omit<ComponentProps<"section">, "title"> {
  tone: BlockTone;
  /** Small caps line above the title ("Your cart", "Step 1 of 3", an order number). */
  eyebrow?: ReactNode;
  /** The big title on the colour block (first line). */
  title: ReactNode;
  /** Optional second line; handwritten (Dancing Script) unless `titleScript` says otherwise (D89). */
  title2?: ReactNode;
  /** Which title line is handwritten. Default `line2` when there is a second line. */
  titleScript?: "line1" | "line2" | "none";
  /** One title line on a brush swipe (paper on mango blocks, mango elsewhere). */
  titleBrush?: "line1" | "line2";
  /** The small dash doodle at the title's corner. Default true. */
  doodle?: boolean;
  /** Rounded corners and a shadow, for showing the screen as a card (playground). Default false: full bleed. */
  framed?: boolean;
  slot: IllustrationSlot;
  avatarSize?: "sm" | "md";
  avatarFocus?: string;
  /** At most one or two short labels on the avatar's edge. */
  stickers?: readonly BlockSticker[];
  /** Heading inside the paper sheet. */
  sheetTitle?: ReactNode;
  /** Body of the sheet: a sentence, buttons, a form. */
  children?: ReactNode;
  /** Default 1: these are usually the page's main heading. */
  headingLevel?: HeadingLevel;
  /** Default true: the title says what the picture shows. */
  illustrationDecorative?: boolean;
  priority?: boolean;
}

/**
 * Hero, empty, error and onboarding screens: a full colour block with big display type, a ringed avatar that
 * breaks over the top of a paper sheet, and the sheet with the message and the one action.
 */
export function ColourBlockScreen({
  tone,
  eyebrow,
  title,
  title2,
  titleScript,
  titleBrush,
  doodle = true,
  framed = false,
  slot,
  avatarSize = "md",
  avatarFocus,
  stickers = [],
  sheetTitle,
  children,
  headingLevel = 1,
  illustrationDecorative = true,
  priority = false,
  className,
  ...props
}: ColourBlockScreenProps) {
  const titleId = useId();
  const md = avatarSize === "md";
  return (
    <section
      aria-labelledby={titleId}
      data-tone={tone}
      className={cn(
        "flex flex-col overflow-hidden",
        framed && "shadow-soft-md rounded-[34px]",
        blockTones[tone],
        className,
      )}
      {...props}
    >
      {/* Phones: title, then the avatar centred over the sheet. From md: a band with the title on the left and the
          avatar on the right, breaking over the sheet (web mockup). */}
      <div className="flex flex-col items-center px-5 pt-6 sm:px-8 sm:pt-8 md:grid md:grid-cols-[1fr_auto] md:items-end md:gap-6 md:px-14 md:pt-8">
        <div className="grid gap-1 text-center md:pb-[70px] md:text-left">
          {eyebrow && (
            <p className="text-xs font-bold tracking-[0.1em] uppercase opacity-80">{eyebrow}</p>
          )}
          {createElement(
            heading(headingLevel),
            {
              id: titleId,
              className: cn(
                "font-display justify-self-center text-[2.25rem] leading-[0.98] font-extrabold tracking-[-0.01em] text-balance sm:text-5xl md:justify-self-start lg:text-[3.625rem]",
                doodle && "fo-doodle px-[0.9em] md:pl-0",
              ),
            },
            headingLines({
              line1: title,
              line2: title2,
              script: titleScript,
              brush: titleBrush,
              brushTone: tone === "mango" ? "paper" : "mango",
            }),
          )}
        </div>
        <div
          className={cn(
            "relative z-[1] mt-4 md:mt-0",
            md ? "-mb-[86px] md:-mb-[110px]" : "-mb-16 md:-mb-[90px]",
          )}
        >
          <AvatarCircle
            slot={slot}
            size={avatarSize}
            focus={avatarFocus}
            decorative={illustrationDecorative}
            priority={priority}
            className={md ? "md:size-[250px]" : "md:size-[200px]"}
          />
          {stickers.map((s, i) => (
            <Sticker
              key={`${s.side}-${i}`}
              tone={s.tone ?? "mango"}
              size="sm"
              tilt={s.side === "left" ? "left" : "right"}
              className={cn("absolute top-6 z-[2]", s.side === "left" ? "-left-14" : "-right-14")}
            >
              {s.label}
            </Sticker>
          ))}
        </div>
      </div>
      <div
        className={cn(
          "bg-surface-paper text-text grid flex-1 content-start justify-items-center gap-3 rounded-t-[34px] px-5 pb-6 text-center sm:px-8 sm:pb-8",
          "md:justify-items-start md:rounded-t-[40px] md:px-14 md:pt-[60px] md:text-left",
          md ? "pt-[104px]" : "pt-20",
        )}
      >
        {sheetTitle &&
          createElement(
            heading(Math.min(headingLevel + 1, 4) as HeadingLevel),
            {
              className:
                "font-display text-2xl leading-none font-extrabold tracking-[-0.01em] text-balance md:text-3xl",
            },
            sheetTitle,
          )}
        {children}
      </div>
    </section>
  );
}

// ---------- BentoTile ----------
export type BentoTone = BlockTone | "paper";

const bentoTones: Record<BentoTone, string> = {
  ...blockTones,
  paper: "bg-surface-paper text-text",
};

export interface BentoTileProps extends Omit<ComponentProps<"div">, "title"> {
  tone?: BentoTone;
  title: ReactNode;
  description?: ReactNode;
  /** Short label pill on top ("Caught 5:40 am"). */
  chip?: ReactNode;
  /** Character peeking from the bottom-right corner. */
  slot?: IllustrationSlot;
  slotFocus?: string;
  /** Spans both columns, taller, bigger title. */
  wide?: boolean;
  /** Render the single child (a link) as the tile; title, description and peek are placed inside it. */
  asChild?: boolean;
  headingLevel?: HeadingLevel;
}

/** One tile of the home bento grid: a colour, a chunky title, one line, and a character peeking from a corner. */
export function BentoTile({
  tone = "teal",
  title,
  description,
  chip,
  slot,
  slotFocus,
  wide = false,
  asChild = false,
  headingLevel = 3,
  className,
  children,
  ...props
}: BentoTileProps) {
  const content = (
    <>
      <span className="relative z-[1] grid content-start gap-1.5">
        {chip && (
          <span className="bg-mango text-on-mango justify-self-start rounded-full px-2.5 py-1 text-xs font-bold">
            {chip}
          </span>
        )}
        {createElement(
          heading(headingLevel),
          {
            className: cn(
              "font-display leading-none font-extrabold tracking-[-0.01em] text-balance",
              wide ? "max-w-[60%] text-2xl sm:text-3xl" : "text-lg sm:text-xl",
            ),
          },
          title,
        )}
      </span>
      {description && (
        <span
          className={cn(
            "relative z-[1] text-sm font-semibold",
            wide ? "max-w-[55%]" : "max-w-[70%]",
          )}
        >
          {description}
        </span>
      )}
      {slot && (
        <AvatarCircle
          slot={slot}
          size="xs"
          focus={slotFocus}
          className={cn(
            "absolute",
            wide ? "-right-4 -bottom-7 size-[150px] sm:size-[180px]" : "-right-5 -bottom-5",
          )}
        />
      )}
    </>
  );
  const classes = cn(
    "relative grid min-h-[130px] content-between gap-3 overflow-hidden rounded-3xl p-4",
    wide && "col-span-2 min-h-[150px] sm:min-h-[190px] sm:p-6",
    bentoTones[tone],
    asChild && cn("transition-transform motion-safe:hover:-translate-y-0.5", focusRing),
    className,
  );
  if (asChild) {
    return (
      <Slot.Root className={classes} {...props}>
        <Slot.Slottable>{children}</Slot.Slottable>
        {content}
      </Slot.Root>
    );
  }
  return (
    <div className={classes} {...props}>
      {content}
      {children}
    </div>
  );
}

// ---------- CategoryBubble ----------
const bubbleTones = {
  teal: "bg-teal-soft",
  mango: "bg-mango-soft",
  ice: "bg-ice-soft",
  coral: "bg-accent-coral-subtle",
} as const;
export type BubbleTone = keyof typeof bubbleTones;

export interface CategoryBubbleProps extends Omit<ComponentProps<"a">, "children"> {
  label: ReactNode;
  /** Picture or icon inside the circle (decorative; the label names the category). */
  icon?: ReactNode;
  tone?: BubbleTone;
  /** The current category: ink ring and offset shadow, `aria-current="page"`. */
  selected?: boolean;
  /** Render the single child (e.g. a Next.js Link) with the bubble's styles. */
  asChild?: boolean;
  children?: ReactNode;
}

/** A round category picker with a label underneath. Put several in a horizontally scrolling row. */
export function CategoryBubble({
  label,
  icon,
  tone = "teal",
  selected = false,
  asChild = false,
  className,
  children,
  ...props
}: CategoryBubbleProps) {
  const Comp = asChild ? Slot.Root : "a";
  return (
    <Comp
      aria-current={selected ? "page" : undefined}
      data-selected={selected || undefined}
      className={cn(
        "text-text grid w-[72px] shrink-0 justify-items-center gap-1.5 rounded-2xl text-center text-xs font-bold",
        focusRing,
        className,
      )}
      {...props}
    >
      {asChild && <Slot.Slottable>{children}</Slot.Slottable>}
      <span
        aria-hidden="true"
        className={cn(
          "grid size-[60px] place-items-center overflow-hidden rounded-full border-2 border-transparent [&_img]:size-full [&_img]:object-cover [&_svg]:size-7",
          bubbleTones[tone],
          selected && "border-outline-ink shadow-sticker-sm",
        )}
      >
        {icon}
      </span>
      <span className="leading-tight">{label}</span>
    </Comp>
  );
}

// ---------- FilterChip ----------
export interface FilterChipProps extends ComponentProps<"button"> {
  /** Toggle state; pressed chips are filled with ink. Leave undefined for a plain chip button (e.g. "More filters"). */
  pressed?: boolean;
  asChild?: boolean;
  icon?: ReactNode;
}

/** A small toggle chip for filters and sort ("Cleaned free", "Under ₹500"). */
export function FilterChip({
  pressed,
  asChild = false,
  icon,
  className,
  children,
  type,
  ...props
}: FilterChipProps) {
  const classes = cn(
    "inline-flex h-9 shrink-0 cursor-pointer items-center gap-1.5 rounded-full border px-3.5 text-sm font-semibold whitespace-nowrap [&_svg]:size-4",
    pressed
      ? "border-outline-ink bg-outline-ink text-on-outline-ink"
      : "border-border bg-surface-paper text-text hover:border-border-strong",
    focusRing,
    className,
  );
  if (asChild) {
    return (
      <Slot.Root className={classes} data-pressed={pressed || undefined} {...props}>
        {children}
      </Slot.Root>
    );
  }
  return (
    <button type={type ?? "button"} aria-pressed={pressed} className={classes} {...props}>
      {icon}
      {children}
    </button>
  );
}

// ---------- CartAddControl ----------
export interface CartAddControlProps {
  /** Plain-text item name for button labels. */
  label: string;
  /** Quantity in the cart as the server last reported it; 0 shows ADD. */
  quantity?: number;
  onAdd?: () => void;
  /** ADD as a link (the item needs a choice first); its name is "Choose options for X". */
  addHref?: string;
  renderLink?: (props: { href: string; className: string; children: ReactNode }) => ReactNode;
  onQuantityChange?: (quantity: number) => void;
  min?: number;
  max?: number;
  step?: number;
  formatQuantity?: (value: number) => ReactNode;
  pending?: boolean;
  unavailableLabel?: ReactNode;
  className?: string;
}

/**
 * The ADD button of a product card, which becomes a − n + stepper once the item is in the cart (or a plain label
 * when it can't be bought). Shared by the app card (`ProductCard`) and the website card (`FoodCard`).
 */
export function CartAddControl({
  label,
  quantity = 0,
  onAdd,
  addHref,
  renderLink,
  onQuantityChange,
  min = 0,
  max,
  step = 1,
  formatQuantity,
  pending = false,
  unavailableLabel,
  className,
}: CartAddControlProps) {
  const addClass = cn(
    "border-border bg-surface-paper text-accent-text shadow-soft-sm hover:bg-teal-soft hover:border-accent inline-flex h-9 min-w-[84px] cursor-pointer items-center justify-center rounded-[10px] border-[1.5px] px-5 text-sm font-extrabold tracking-[0.04em] disabled:cursor-wait disabled:opacity-70",
    focusRing,
    className,
  );
  if (unavailableLabel) {
    return (
      <span
        className={cn(
          "border-border bg-surface-paper text-text-muted rounded-[10px] border px-3 py-1.5 text-xs font-bold whitespace-nowrap",
          className,
        )}
      >
        {unavailableLabel}
      </span>
    );
  }
  if (quantity > 0 && onQuantityChange) {
    return (
      <QuantityStepper
        variant="solid"
        size="sm"
        allowRemove
        value={quantity}
        onChange={onQuantityChange}
        min={min}
        max={max}
        step={step}
        formatValue={formatQuantity}
        label={label}
        pending={pending}
        className={className}
      />
    );
  }
  if (onAdd) {
    return (
      <button
        type="button"
        onClick={onAdd}
        disabled={pending}
        aria-busy={pending || undefined}
        aria-label={`Add ${label} to cart`}
        className={addClass}
      >
        {pending ? <Spinner size="sm" label="Adding" /> : "ADD"}
      </button>
    );
  }
  if (addHref) {
    const children = (
      <>
        <span aria-hidden="true">ADD</span>
        <span className="sr-only">{`Choose options for ${label}`}</span>
      </>
    );
    return renderLink ? (
      renderLink({ href: addHref, className: addClass, children })
    ) : (
      <a href={addHref} className={addClass}>
        {children}
      </a>
    );
  }
  return null;
}

// ---------- ProductCard ----------
const pictureTones = bubbleTones;

export interface ProductCardProps extends Omit<ComponentProps<"article">, "title"> {
  name: ReactNode;
  /** Product page link; render the title as a link with `renderLink` for client routers. */
  href: string;
  renderLink?: (props: { href: string; className: string; children: ReactNode }) => ReactNode;
  /** The picture: an <img> or our silhouette SVG. Decorative; the name says what it is. */
  picture?: ReactNode;
  pictureTone?: BubbleTone;
  /** Small label on the picture ("Caught 5:40 am", "Bestseller"). */
  badge?: ReactNode;
  /** Pack or weight line ("500 g · 6–8 pcs"). */
  detail?: ReactNode;
  /** The server's price, already formatted (normally a `PriceTag`). */
  price?: ReactNode;
  /** Quantity of this item in the cart as the server last reported it; 0 shows ADD. */
  quantity?: number;
  /** ADD pressed. The app sends the add and shows the server's response. */
  onAdd?: () => void;
  /**
   * When the item needs a choice first (size, cut), ADD is a link to this page instead of a button. Its name is
   * "Choose options for X"; it goes through `renderLink` too.
   */
  addHref?: string;
  /** Stepper pressed: the proposed new quantity (0 = remove). Bounds come from the server. */
  onQuantityChange?: (quantity: number) => void;
  min?: number;
  max?: number;
  step?: number;
  formatQuantity?: (value: number) => ReactNode;
  /** Plain-text name for button labels ("Add Seer fish steaks to cart"). */
  label: string;
  pending?: boolean;
  /** When set, the item can't be bought here; shown instead of ADD (e.g. "Out of stock"). */
  unavailableLabel?: ReactNode;
  headingLevel?: HeadingLevel;
}

/**
 * Two-column shop card: tinted picture with a badge, an ADD button sitting on the picture's bottom edge that
 * becomes a − n + stepper once the item is in the cart, then name, detail and the server's price.
 */
export function ProductCard({
  name,
  href,
  renderLink,
  picture,
  pictureTone = "teal",
  badge,
  detail,
  price,
  quantity = 0,
  onAdd,
  addHref,
  onQuantityChange,
  min = 0,
  max,
  step = 1,
  formatQuantity,
  label,
  pending = false,
  unavailableLabel,
  headingLevel = 3,
  className,
  ...props
}: ProductCardProps) {
  const linkClass = cn(
    "rounded-sm after:absolute after:inset-0 after:content-[''] hover:underline",
    focusRing,
  );
  const link = renderLink ? (
    renderLink({ href, className: linkClass, children: name })
  ) : (
    <a href={href} className={linkClass}>
      {name}
    </a>
  );
  const action = (
    <CartAddControl
      label={label}
      quantity={quantity}
      onAdd={onAdd}
      addHref={addHref}
      renderLink={renderLink}
      onQuantityChange={onQuantityChange}
      min={min}
      max={max}
      step={step}
      formatQuantity={formatQuantity}
      pending={pending}
      unavailableLabel={unavailableLabel}
    />
  );
  const hasAction = Boolean(
    unavailableLabel || onAdd || addHref || (quantity > 0 && onQuantityChange),
  );
  return (
    <article
      className={cn(
        "bg-surface-paper text-text shadow-soft-md relative grid min-w-0 rounded-[20px]",
        className,
      )}
      {...props}
    >
      <div
        className={cn(
          "relative grid h-32 place-items-center rounded-t-[20px] sm:h-40 [&_img]:size-full [&_img]:rounded-t-[20px] [&_img]:object-cover",
          pictureTones[pictureTone],
        )}
      >
        <span aria-hidden="true" className="absolute inset-0 grid place-items-center">
          {picture}
        </span>
        {badge && (
          <span className="bg-surface-paper text-text absolute top-2 left-2 z-[1] rounded-full px-2 py-0.5 text-[0.6875rem] font-bold">
            {badge}
          </span>
        )}
        {hasAction && (
          <div className="absolute bottom-0 left-1/2 z-[2] -translate-x-1/2 translate-y-1/2">
            {action}
          </div>
        )}
      </div>
      <div className="grid gap-0.5 px-3 pt-6 pb-3">
        {createElement(
          heading(headingLevel),
          {
            className:
              "font-display text-base leading-[1.1] font-extrabold tracking-[-0.01em] text-balance",
          },
          link,
        )}
        {detail && <p className="text-text-muted text-xs">{detail}</p>}
        {price && <div className="font-display mt-1">{price}</div>}
      </div>
    </article>
  );
}

// ---------- FloatingCartBar ----------
export interface FloatingCartBarProps extends Omit<ComponentProps<"div">, "children"> {
  /** Server-derived summary, e.g. "2 items added". */
  summary: ReactNode;
  /** The cart total exactly as the server sent it, already formatted. */
  total?: ReactNode;
  /** Default "View cart". */
  actionLabel?: ReactNode;
  /** Cart page. */
  href: string;
  /** Render the link with a client router (Next.js Link). */
  renderLink?: (props: { href: string; className: string; children: ReactNode }) => ReactNode;
}

/**
 * The floating teal "View cart" bar shown once the cart has items. On phones it sits above the tab bar; from
 * md up it floats 24 px from the bottom.
 */
export function FloatingCartBar({
  summary,
  total,
  actionLabel = "View cart",
  href,
  renderLink,
  className,
  ...props
}: FloatingCartBarProps) {
  const actionClass = cn(
    "inline-flex items-center gap-1 rounded-xl px-2 py-1.5 text-sm font-extrabold whitespace-nowrap hover:bg-[rgb(255_255_255_/_0.12)] [&_svg]:size-4",
    focusRing,
  );
  const actionChildren = (
    <>
      {actionLabel}
      <ChevronRight aria-hidden="true" />
    </>
  );
  return (
    <div
      role="region"
      aria-label="Cart summary"
      className={cn(
        "bg-accent text-on-accent fixed inset-x-3 bottom-[calc(76px+env(safe-area-inset-bottom,0px))] z-[1100] mx-auto flex max-w-xl items-center justify-between gap-3 rounded-2xl py-2.5 pr-3 pl-4 shadow-[0_10px_24px_rgb(11_125_112_/_0.35)] md:right-7 md:bottom-5 md:left-auto md:mx-0 md:w-[min(420px,calc(100%-56px))]",
        "motion-safe:animate-fo-slide-in-bottom",
        className,
      )}
      {...props}
    >
      <div className="min-w-0">
        <p className="text-[0.6875rem] font-semibold tracking-[0.04em] uppercase opacity-90">
          {summary}
        </p>
        {total && (
          <p className="font-display text-lg leading-tight font-extrabold tracking-[-0.01em] tabular-nums">
            {total}
          </p>
        )}
      </div>
      {renderLink ? (
        renderLink({ href, className: actionClass, children: actionChildren })
      ) : (
        <a href={href} className={actionClass}>
          {actionChildren}
        </a>
      )}
    </div>
  );
}

// ---------- TabBar ----------
export interface TabBarItem {
  href: string;
  label: string;
  icon: ReactNode;
  /** A count from the server (cart lines); hidden when 0 or missing. */
  badge?: number;
}

export interface TabBarProps extends Omit<ComponentProps<"nav">, "children"> {
  items: readonly TabBarItem[];
  /** href of the current tab. */
  current?: string;
  renderLink?: (props: {
    href: string;
    className: string;
    "aria-current"?: "page";
    children: ReactNode;
  }) => ReactNode;
  /** Accessible name of the navigation. Default "Main". */
  label?: string;
  /**
   * `bottom` (default): fixed to the bottom on phones, hidden from md up. `top`: the same bar moved into the
   * website's top header from md up (underline under the active tab), hidden on phones.
   */
  placement?: "bottom" | "top";
}

/**
 * The food-app bottom tab bar: icon over label, the active tab in a teal pill with a notch on top, a coral count
 * badge on the cart. Hidden from md up, where the app shows the same items in its top header.
 */
export function TabBar({
  items,
  current,
  renderLink,
  label = "Main",
  placement = "bottom",
  className,
  ...props
}: TabBarProps) {
  const top = placement === "top";
  return (
    <nav
      aria-label={label}
      data-placement={placement}
      className={cn(
        top
          ? "hidden items-center gap-1.5 md:flex"
          : "border-border bg-surface-paper fixed inset-x-0 bottom-0 z-[1100] grid border-t px-1 pt-1.5 pb-[calc(10px+env(safe-area-inset-bottom,0px))] shadow-[0_-6px_18px_rgb(40_30_10_/_0.06)] md:hidden",
        className,
      )}
      style={top ? undefined : { gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}
      {...props}
    >
      {items.map((item) => {
        const active = item.href === current;
        const className = cn(
          "relative isolate grid justify-items-center gap-0.5 rounded-lg pt-1 text-[0.6875rem] [&_svg]:size-[22px]",
          top && "min-w-16 py-1",
          active
            ? "text-accent-text font-extrabold [&_svg]:stroke-[2.4]"
            : "text-text-muted font-semibold",
          focusRing,
        );
        const children = (
          <>
            {active && (
              <>
                <span
                  aria-hidden="true"
                  className={cn(
                    "bg-accent absolute inset-x-[28%] h-[3.5px]",
                    top ? "-bottom-[9px] rounded-t" : "-top-[7px] rounded-b",
                  )}
                />
                <span
                  aria-hidden="true"
                  className={cn(
                    "bg-teal-soft absolute -z-10 h-7 w-11 rounded-full",
                    top ? "top-0.5" : "top-0",
                  )}
                />
              </>
            )}
            <span aria-hidden="true">{item.icon}</span>
            {item.label}
            {item.badge != null && item.badge > 0 && (
              <>
                <span
                  aria-hidden="true"
                  className="border-surface-paper bg-accent-coral text-on-accent-coral absolute top-0.5 right-[calc(50%-20px)] min-w-4 rounded-full border-2 px-1 text-center text-[0.625rem] leading-3 font-extrabold"
                >
                  {item.badge}
                </span>
                <span className="sr-only">{` (${item.badge} in cart)`}</span>
              </>
            )}
          </>
        );
        const ariaCurrent = active ? ("page" as const) : undefined;
        return (
          <span key={item.href} className="contents">
            {renderLink ? (
              renderLink({ href: item.href, className, "aria-current": ariaCurrent, children })
            ) : (
              <a href={item.href} className={className} aria-current={ariaCurrent}>
                {children}
              </a>
            )}
          </span>
        );
      })}
    </nav>
  );
}

// ---------- OptionTile (cut choice, delivery slot) ----------
export const optionTileVariants = cva(
  [
    "grid cursor-pointer gap-0.5 rounded-[13px] border-[1.5px] px-3 py-2 text-left text-sm font-bold",
    "has-[input:focus-visible]:outline-ring has-[input:focus-visible]:outline-2 has-[input:focus-visible]:outline-offset-2",
    "has-[input:disabled]:cursor-not-allowed has-[input:disabled]:opacity-50",
  ],
  {
    variants: {
      look: {
        /** Cut choice: teal tint when chosen. */
        teal: "border-border has-[input:checked]:border-accent has-[input:checked]:bg-teal-soft has-[input:checked]:shadow-[inset_0_0_0_1px_var(--fo-color-accent)]",
        /** Delivery slot: ink fill when chosen. */
        ink: "border-border has-[input:checked]:border-outline-ink has-[input:checked]:bg-outline-ink has-[input:checked]:text-on-outline-ink text-center",
      },
    },
    defaultVariants: { look: "teal" },
  },
);

export interface OptionTileProps
  extends
    Omit<ComponentProps<"input">, "type" | "children">,
    VariantProps<typeof optionTileVariants> {
  label: ReactNode;
  description?: ReactNode;
}

/** A radio drawn as a tile (cut choice, delivery slot). Group several inside a `fieldset` with a legend. */
export function OptionTile({ label, description, look, className, ...props }: OptionTileProps) {
  const id = useId();
  return (
    <label className={cn(optionTileVariants({ look }), className)}>
      <input
        type="radio"
        className="sr-only"
        aria-labelledby={`${id}-label`}
        aria-describedby={description ? `${id}-description` : undefined}
        {...props}
      />
      <span id={`${id}-label`}>{label}</span>
      {description && (
        <span id={`${id}-description`} className="text-xs font-medium opacity-75">
          {description}
        </span>
      )}
    </label>
  );
}

// ---------- CouponStrip ----------
export interface CouponStripProps extends Omit<ComponentProps<"div">, "title"> {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}

/** The dashed mango strip for an applied coupon or a coupon prompt. Shows the server's saving as given. */
export function CouponStrip({
  icon,
  title,
  description,
  action,
  className,
  ...props
}: CouponStripProps) {
  return (
    <div
      className={cn(
        "border-mango bg-mango-soft text-text flex items-center gap-3 rounded-[14px] border-[1.5px] border-dashed px-3 py-2.5 text-sm",
        className,
      )}
      {...props}
    >
      {icon && (
        <span aria-hidden="true" className="text-accent-coral-text [&_svg]:size-5">
          {icon}
        </span>
      )}
      <div className="min-w-0 flex-1">
        <p className="font-display font-extrabold">{title}</p>
        {description && <p className="text-text-muted text-xs">{description}</p>}
      </div>
      {action}
    </div>
  );
}
