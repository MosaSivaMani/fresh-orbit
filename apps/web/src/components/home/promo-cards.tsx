import { PillLink, type PillTone, PromoCard, PromoCardGrid, type PromoTone } from "@fresh/ui";
import { ArrowRight } from "lucide-react";
import Image from "next/image";
import Link from "next/link";

export interface HomePromo {
  title: string;
  line: string;
  action: string;
  href: string;
  picture: string;
  /** What the picture shows, for screen readers. */
  alt: string;
  tone: PromoTone;
  pill: PillTone;
}

/** The three store promises after the owner's reference (D188). Fixed marketing copy; no prices or numbers. */
export const homePromoCards: readonly HomePromo[] = [
  {
    title: "Today's Fresh Catch",
    line: "Straight from local boats",
    action: "Shop fresh fish",
    href: "/shop",
    picture: "/home/promo-fresh-catch.png",
    alt: "Seer fish, prawns and crabs on ice",
    tone: "teal",
    pill: "dark",
  },
  {
    title: "Cleaned Your Way",
    line: "Whole · Steaks · Curry cut",
    action: "Learn more",
    href: "/support",
    picture: "/home/promo-cleaned.png",
    alt: "Fish steaks and curry-cut pieces on a banana leaf",
    tone: "coral",
    pill: "coral",
  },
  {
    title: "Chilled & Delivered Fresh",
    line: "From our store to your door",
    action: "See how it works",
    href: "/support/delivery-policy",
    picture: "/home/promo-delivered.png",
    alt: "Ravi waving from his scooter with a chilled delivery box",
    tone: "sky",
    pill: "light",
  },
];

/** The home's promo cards (D188), from the shared `PromoCard` in `@fresh/ui`. */
export function PromoCards({ cards = homePromoCards }: { cards?: readonly HomePromo[] }) {
  return (
    <section aria-labelledby="home-promises-heading">
      <h2 id="home-promises-heading" className="sr-only">
        Why shop with Fresh Orbitt
      </h2>
      <PromoCardGrid>
        {cards.map((card) => (
          <PromoCard
            key={card.title}
            tone={card.tone}
            title={card.title}
            line={card.line}
            picture={
              <Image
                src={card.picture}
                alt={card.alt}
                fill
                sizes="(min-width: 1024px) 180px, 45vw"
              />
            }
            action={
              <PillLink tone={card.pill} asChild>
                <Link href={card.href}>
                  {card.action}
                  <ArrowRight aria-hidden="true" />
                </Link>
              </PillLink>
            }
          />
        ))}
      </PromoCardGrid>
    </section>
  );
}
