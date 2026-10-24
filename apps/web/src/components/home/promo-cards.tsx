import { cn, focusRing } from "@fresh/ui";
import { ArrowRight } from "lucide-react";
import Image from "next/image";
import Link from "next/link";

export interface PromoCard {
  title: string;
  line: string;
  action: string;
  href: string;
  picture: string;
  /** What the picture shows, for screen readers. */
  alt: string;
  tone: "teal" | "coral" | "sky";
}

/** The three store promises after the owner's reference (D188). Fixed marketing copy; no prices or numbers. */
export const homePromoCards: readonly PromoCard[] = [
  {
    title: "Today's Fresh Catch",
    line: "Straight from local boats",
    action: "Shop fresh fish",
    href: "/shop",
    picture: "/home/promo-fresh-catch.png",
    alt: "Seer fish, prawns and crabs on ice",
    tone: "teal",
  },
  {
    title: "Cleaned Your Way",
    line: "Whole · Steaks · Curry cut",
    action: "Learn more",
    href: "/support",
    picture: "/home/promo-cleaned.png",
    alt: "Fish steaks and curry-cut pieces on a banana leaf",
    tone: "coral",
  },
  {
    title: "Chilled & Delivered Fresh",
    line: "From our store to your door",
    action: "See how it works",
    href: "/support/delivery-policy",
    picture: "/home/promo-delivered.png",
    alt: "Ravi waving from his scooter with a chilled delivery box",
    tone: "sky",
  },
];

const tones = {
  teal: { card: "bg-teal-soft", pill: "bg-primary text-on-primary" },
  coral: { card: "bg-accent-coral-subtle", pill: "bg-surface text-accent-coral-text" },
  sky: { card: "bg-ice-soft", pill: "bg-surface text-primary" },
} as const;

/**
 * Three promo cards in a row (D188): a tinted card with the picture on the left, the title, a line and a pill link.
 * One column on phones, two from `sm`, three from `lg`.
 */
export function PromoCards({ cards = homePromoCards }: { cards?: readonly PromoCard[] }) {
  return (
    <section aria-labelledby="home-promises-heading">
      <h2 id="home-promises-heading" className="sr-only">
        Why shop with Fresh Orbitt
      </h2>
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {cards.map((card) => (
          <li
            key={card.title}
            className={cn(
              "flex min-h-40 overflow-hidden rounded-2xl shadow-[0_6px_18px_rgb(10_38_48/0.08)]",
              tones[card.tone].card,
            )}
          >
            <div className="relative w-[44%] shrink-0">
              <Image
                src={card.picture}
                alt={card.alt}
                fill
                sizes="(min-width: 1024px) 180px, 45vw"
                className="object-contain object-center p-2"
              />
            </div>
            <div className="flex flex-1 flex-col justify-center gap-1 py-4 pr-4">
              <h3 className="font-display text-text text-xl leading-tight font-extrabold">
                {card.title}
              </h3>
              <p className="text-text-muted text-sm">{card.line}</p>
              <Link
                href={card.href}
                className={cn(
                  "mt-2 inline-flex items-center gap-1.5 self-start rounded-full px-4 py-2 text-sm font-bold shadow-xs",
                  tones[card.tone].pill,
                  focusRing,
                )}
              >
                {card.action}
                <ArrowRight aria-hidden="true" className="size-4" />
              </Link>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
