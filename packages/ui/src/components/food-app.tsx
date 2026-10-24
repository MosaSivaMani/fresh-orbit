// "Food-app" website components (D95, docs/design/mockups/web-food-app.html version 2): the coral hero with
// tilted character photos, white promise cards, round "What's on your mind?" circles, picture cards with an
// offer line on a dark gradient, the get-the-app band, page bands and character states.
//
// Phone and desktop widths use these same components (no separate mobile design). None of them compute business
// values (CLAUDE.md rule 4): prices, offers, freshness lines and quantities are passed in as the server gave them.
import { Slot } from "radix-ui";
import { type ComponentProps, type ReactNode, createElement } from "react";
import { cn, focusRing } from "../lib/cn";
import {
  AvatarCircle,
  type AvatarSize,
  CartAddControl,
  type CartAddControlProps,
} from "./bold-blocks";
import { type IllustrationSlot, Illustration } from "./illustrated";

type HeadingLevel = 1 | 2 | 3 | 4;
const heading = (level: HeadingLevel) => `h${level}` as const;

// ---------- FoodHero ----------
export interface FoodHeroProps extends ComponentProps<"section"> {
  /** Tilted character photo at the left edge (hidden on phones). */
  leftSlot?: IllustrationSlot;
  /** Tilted character photo at the right edge (hidden on phones). */
  rightSlot?: IllustrationSlot;
  /** CSS object-position for each photo. */
  leftFocus?: string;
  rightFocus?: string;
}

/**
 * The coral hero: centred headline, the pincode and search boxes and the promise cards (passed as children), with
 * two character photos tilted at the sides from `md` up. The photos are decorative.
 */
export function FoodHero({
  leftSlot,
  rightSlot,
  leftFocus = "50% 30%",
  rightFocus = "50% 30%",
  className,
  children,
  ...props
}: FoodHeroProps) {
  const photo =
    "absolute top-8 hidden h-[300px] w-[220px] overflow-hidden rounded-[28px] shadow-[0_18px_40px_rgb(0_0_0/0.2)] lg:block";
  return (
    <section
      className={cn(
        "bg-block-coral text-on-block-coral relative overflow-hidden px-4 pt-7 pb-12 text-center sm:px-8 sm:pb-14",
        className,
      )}
      {...props}
    >
      {leftSlot && (
        <div aria-hidden="true" className={cn(photo, "-left-10 -rotate-[8deg]")}>
          <Illustration
            slot={leftSlot}
            size="fill"
            fit="cover"
            decorative
            className="aspect-auto size-full rounded-none"
            style={{ objectPosition: leftFocus }}
          />
        </div>
      )}
      {rightSlot && (
        <div aria-hidden="true" className={cn(photo, "-right-10 rotate-[8deg]")}>
          <Illustration
            slot={rightSlot}
            size="fill"
            fit="cover"
            decorative
            className="aspect-auto size-full rounded-none"
            style={{ objectPosition: rightFocus }}
          />
        </div>
      )}
      <div className="relative z-[1] mx-auto grid max-w-[760px] justify-items-center gap-5 sm:gap-6">
        {children}
      </div>
    </section>
  );
}

// ---------- PromiseCard ----------
export interface PromiseCardProps extends Omit<ComponentProps<"div">, "title"> {
  /** The big word ("FISH"). */
  title: ReactNode;
  /** The line under it ("FROM THE BOATS"). */
  line?: ReactNode;
  /** The small coral caps label at the bottom ("CAUGHT TODAY"). */
  label?: ReactNode;
}

/** A white promise card in the hero. Store policy text only; never a number the server didn't give. */
export function PromiseCard({ title, line, label, className, ...props }: PromiseCardProps) {
  return (
    <div
      className={cn(
        "bg-surface text-text font-display grid min-h-[104px] content-start rounded-[22px] px-[18px] py-4 text-left text-[15px] leading-[1.15] font-extrabold shadow-[0_8px_18px_rgb(0_0_0/0.12)] sm:min-h-[120px]",
        className,
      )}
      {...props}
    >
      <span className="text-[22px]">{title}</span>
      {line && <span>{line}</span>}
      {label && (
        <span className="text-accent-coral-text mt-1.5 font-sans text-[11px] font-bold tracking-[0.06em]">
          {label}
        </span>
      )}
    </div>
  );
}

// ---------- CategoryCircle ----------
export interface CategoryCircleProps extends Omit<ComponentProps<"a">, "children"> {
  label: ReactNode;
  /** Picture or icon inside the circle (decorative; the label names the category). */
  icon?: ReactNode;
  /** The current category: ink ring and `aria-current="page"`. */
  selected?: boolean;
  /** Render the single child (e.g. a Next.js Link) with these styles. */
  asChild?: boolean;
  children?: ReactNode;
}

/** A round "What's on your mind?" category: a soft circle with the picture and a muted label underneath. */
export function CategoryCircle({
  label,
  icon,
  selected = false,
  asChild = false,
  className,
  children,
  ...props
}: CategoryCircleProps) {
  const Comp = asChild ? Slot.Root : "a";
  return (
    <Comp
      aria-current={selected ? "page" : undefined}
      data-selected={selected || undefined}
      className={cn(
        "group grid w-[88px] shrink-0 justify-items-center gap-1.5 rounded-2xl text-center sm:w-[110px]",
        focusRing,
        className,
      )}
      {...props}
    >
      {asChild && <Slot.Slottable>{children}</Slot.Slottable>}
      <span
        aria-hidden="true"
        className={cn(
          "bg-surface-sand grid size-[88px] place-items-center overflow-hidden rounded-full border-[3px] border-transparent transition-transform group-hover:scale-[1.03] motion-reduce:transition-none sm:size-[110px] [&_img]:size-full [&_img]:object-cover [&_svg]:size-9",
          selected && "border-outline-ink",
        )}
      >
        {icon}
      </span>
      <span
        className={cn(
          "text-sm leading-tight font-semibold",
          selected ? "text-text" : "text-text-muted group-hover:text-text",
        )}
      >
        {label}
      </span>
    </Comp>
  );
}

// ---------- FoodCard ----------
const pictureTones = {
  teal: "bg-teal-soft",
  mango: "bg-mango-soft",
  ice: "bg-ice-soft",
  coral: "bg-accent-coral-subtle",
} as const;
export type FoodCardTone = keyof typeof pictureTones;

export interface FoodCardProps
  extends Omit<ComponentProps<"article">, "title">, Omit<CartAddControlProps, "className"> {
  name: ReactNode;
  /** Product page; the name is a link covering the card. */
  href: string;
  /** The picture: an <img> (decorative; the name says what it is). */
  picture?: ReactNode;
  pictureTone?: FoodCardTone;
  /**
   * The offer line over the bottom of the picture, on a dark gradient ("₹90 OFF"). Only pass what the server's
   * offers say; leave it empty otherwise.
   */
  offer?: ReactNode;
  /** The freshness or delivery line next to the round teal mark (server text). */
  freshness?: ReactNode;
  /** Weight · pieces · serves, where the server has them. */
  meta?: ReactNode;
  /** The server's price, already formatted (normally a `PriceTag`). */
  price?: ReactNode;
  headingLevel?: HeadingLevel;
}

/**
 * The website's picture card (D95): a 16:11 rounded picture with an optional offer line on a dark gradient, the
 * name, a freshness line, weight/pieces/serves, the price and an ADD button that becomes a − n + stepper.
 */
export function FoodCard({
  name,
  href,
  renderLink,
  picture,
  pictureTone = "teal",
  offer,
  freshness,
  meta,
  price,
  headingLevel = 3,
  label,
  quantity,
  onAdd,
  addHref,
  onQuantityChange,
  min,
  max,
  step,
  formatQuantity,
  pending,
  unavailableLabel,
  className,
  ...props
}: FoodCardProps) {
  const linkClass = cn(
    "rounded-sm after:absolute after:inset-0 after:z-[1] after:rounded-[20px] after:content-[''] hover:underline",
    focusRing,
  );
  const link = renderLink ? (
    renderLink({ href, className: linkClass, children: name })
  ) : (
    <a href={href} className={linkClass}>
      {name}
    </a>
  );
  return (
    <article
      className={cn(
        "group text-text relative grid min-w-0 content-start gap-2.5 transition-transform hover:-translate-y-0.5 motion-reduce:transition-none motion-reduce:hover:translate-y-0",
        className,
      )}
      {...props}
    >
      <div
        className={cn(
          "relative grid aspect-[16/11] place-items-center overflow-hidden rounded-[20px] [&_img]:size-full [&_img]:object-cover",
          pictureTones[pictureTone],
        )}
      >
        <span aria-hidden="true" className="contents">
          {picture}
        </span>
        {offer && (
          <span className="font-display absolute inset-x-0 bottom-0 bg-gradient-to-b from-transparent to-black/75 px-3.5 pt-7 pb-2.5 text-left text-lg leading-tight font-extrabold tracking-[0.01em] text-white sm:text-xl">
            {offer}
          </span>
        )}
      </div>
      <div className="grid gap-0.5 px-1.5">
        {createElement(
          heading(headingLevel),
          { className: "font-sans text-base leading-[1.25] font-bold sm:text-[16.5px]" },
          link,
        )}
        {freshness && (
          <p className="flex items-center gap-1.5 text-[13px] font-semibold">
            <span
              aria-hidden="true"
              className="bg-accent text-on-accent grid size-5 shrink-0 place-items-center rounded-full"
            >
              <svg
                viewBox="0 0 24 24"
                className="size-3"
                fill="none"
                stroke="currentColor"
                strokeWidth={3}
              >
                <path d="M5 12.5 10 17l9-10" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </span>
            <span className="min-w-0">{freshness}</span>
          </p>
        )}
        {meta && <p className="text-text-muted text-[12.5px]">{meta}</p>}
        <div className="mt-1.5 flex items-center gap-2">
          {price && <div className="font-display min-w-0">{price}</div>}
          {/* Above the card's link overlay so ADD and the stepper stay clickable. */}
          <div className="relative z-[2] ml-auto">
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
          </div>
        </div>
      </div>
    </article>
  );
}

// ---------- AppBand ----------
export interface AppStoreLink {
  label: string;
  href: string;
  icon?: ReactNode;
}

export interface AppBandProps extends Omit<ComponentProps<"section">, "title"> {
  title: ReactNode;
  description?: ReactNode;
  /** Store links from configuration. When empty, `fallback` shows instead (no invented links). */
  stores?: readonly AppStoreLink[];
  fallback?: ReactNode;
  slot?: IllustrationSlot;
  slotFocus?: string;
  headingLevel?: HeadingLevel;
}

/** The mango-soft "get the app" band with the store buttons and a character in a circle. */
export function AppBand({
  title,
  description,
  stores = [],
  fallback,
  slot,
  slotFocus,
  headingLevel = 2,
  className,
  ...props
}: AppBandProps) {
  return (
    <section
      className={cn(
        "bg-mango-soft text-text grid items-center gap-5 overflow-hidden rounded-[26px] px-5 py-6 sm:grid-cols-[1fr_auto] sm:px-7",
        className,
      )}
      {...props}
    >
      <div className="grid gap-1.5">
        {createElement(
          heading(headingLevel),
          { className: "font-display text-2xl leading-[1.05] font-extrabold sm:text-[30px]" },
          title,
        )}
        {description && <p className="text-text-muted">{description}</p>}
        {stores.length > 0 ? (
          <ul className="mt-2 flex flex-wrap gap-2.5">
            {stores.map((store) => (
              <li key={store.href}>
                <a
                  href={store.href}
                  className={cn(
                    "bg-outline-ink text-on-outline-ink inline-flex items-center gap-1.5 rounded-xl px-4 py-2.5 text-sm font-semibold [&_svg]:size-4",
                    focusRing,
                  )}
                >
                  {store.icon}
                  {store.label}
                </a>
              </li>
            ))}
          </ul>
        ) : (
          fallback && <div className="mt-2 text-sm font-semibold">{fallback}</div>
        )}
      </div>
      {slot && <AvatarCircle slot={slot} size="xs" focus={slotFocus} className="sm:size-40" />}
    </section>
  );
}

// ---------- PageBand ----------
export interface PageBandProps extends Omit<ComponentProps<"section">, "title"> {
  /** Small line above the title ("Your cart", an order number). */
  eyebrow?: ReactNode;
  /** The page heading (use `Heading` content: a word and a `Script`). */
  title: ReactNode;
  description?: ReactNode;
  /** Right-side content (a button, a status). */
  aside?: ReactNode;
  headingLevel?: HeadingLevel;
}

/** The coral band at the top of an inner page: the same colour and type as the home hero, shorter. */
export function PageBand({
  eyebrow,
  title,
  description,
  aside,
  headingLevel = 1,
  className,
  children,
  ...props
}: PageBandProps) {
  return (
    <section className={cn("bg-block-coral text-on-block-coral", className)} {...props}>
      <div className="mx-auto flex max-w-7xl flex-wrap items-end justify-between gap-4 px-4 pt-6 pb-7 sm:px-6 sm:pt-8 sm:pb-9 lg:px-8">
        <div className="grid min-w-0 gap-1">
          {eyebrow && <p className="text-sm font-semibold">{eyebrow}</p>}
          {createElement(
            heading(headingLevel),
            {
              className:
                "font-display text-[2rem] leading-[1.05] font-extrabold tracking-[-0.01em] text-balance sm:text-[2.75rem]",
            },
            title,
          )}
          {description && (
            <p className="max-w-[60ch] text-sm font-medium sm:text-base">{description}</p>
          )}
        </div>
        {aside}
      </div>
      {children}
    </section>
  );
}

// ---------- CharacterState ----------
export interface CharacterStateProps extends Omit<ComponentProps<"section">, "title"> {
  slot: IllustrationSlot;
  slotFocus?: string;
  avatarSize?: Extract<AvatarSize, "xs" | "sm" | "md">;
  title: ReactNode;
  description?: ReactNode;
  /** Buttons or links. */
  actions?: ReactNode;
  headingLevel?: HeadingLevel;
  /** Default true: the title says what the picture shows. */
  illustrationDecorative?: boolean;
}

/** Empty, error and not-found states: the character in a ringed circle, a heading, one line and the actions. */
export function CharacterState({
  slot,
  slotFocus,
  avatarSize = "sm",
  title,
  description,
  actions,
  headingLevel = 2,
  illustrationDecorative = true,
  className,
  children,
  ...props
}: CharacterStateProps) {
  return (
    <section
      className={cn(
        "text-text grid justify-items-center gap-4 px-4 py-10 text-center sm:py-14",
        className,
      )}
      {...props}
    >
      <AvatarCircle
        slot={slot}
        size={avatarSize}
        focus={slotFocus}
        decorative={illustrationDecorative}
      />
      {createElement(
        heading(headingLevel),
        {
          className:
            "font-display text-[1.75rem] leading-[1.05] font-extrabold tracking-[-0.01em] text-balance sm:text-[2.25rem]",
        },
        title,
      )}
      {description && <div className="text-text-muted max-w-[46ch]">{description}</div>}
      {children}
      {actions && <div className="flex flex-wrap justify-center gap-3">{actions}</div>}
    </section>
  );
}
