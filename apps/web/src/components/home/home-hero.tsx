import { FeatureChip, HarbourHero, HeroHeadline } from "@fresh/ui";
import { Fish, Scale, Truck } from "lucide-react";
import Image from "next/image";

export interface HomeHeroProps {
  /**
   * The server's next delivery slot, already formatted ("Sat 25 Oct, 7:00 am – 9:00 am"). Without it the delivery
   * chip says "Pick a slot at checkout".
   */
  nextSlot?: string | null;
  /**
   * An optional photo behind the hero (a path or URL). Null draws the harbour scenery instead. Reserved for a real
   * background picture later (D188).
   */
  heroBackground?: string | null;
}

/**
 * The home hero after the owner's reference (D188), built from the shared `HarbourHero` parts in `@fresh/ui`: Padma
 * with the catch, "Fresh fish. / Cleaned your way. / At your door, in your slot.", three feature chips and "From
 * today's catch to your kitchen". The copy is fixed marketing text; the only live value is the server's next slot
 * (rule 4).
 */
export function HomeHero({ nextSlot = null, heroBackground = null }: HomeHeroProps) {
  return (
    <HarbourHero
      aria-labelledby="home-hero-heading"
      heading={
        <HeroHeadline
          id="home-hero-heading"
          script="Fresh fish."
          brush="Cleaned your way."
          line="At your door, in your slot."
        />
      }
      featuresLabel="Why Fresh Orbitt"
      features={
        <>
          <FeatureChip
            icon={<Fish />}
            title="CLEANED"
            line="Your way"
            detail="Whole · Steaks · Curry Cut"
          />
          <FeatureChip
            icon={<Scale />}
            title="PAY FOR"
            line="Net weight"
            detail="Weighed after cleaning"
          />
          <FeatureChip
            icon={<Truck />}
            title="DELIVERY"
            line="In your slot"
            detail={nextSlot ? `Next slot ${nextSlot}` : "Pick a slot at checkout"}
          />
        </>
      }
      tagline={["From", "today's catch", "to your kitchen"]}
      picture={
        <Image
          src="/home/hero-catch.png"
          alt="Padma smiling and holding a large fresh seer fish over prawns, crabs and fish on ice"
          fill
          priority
          sizes="(min-width: 1024px) 35vw, 440px"
          className="object-cover object-[78%_0%] lg:object-[82%_0%]"
        />
      }
      background={
        heroBackground ? (
          <Image src={heroBackground} alt="" fill priority sizes="100vw" className="object-cover" />
        ) : null
      }
    />
  );
}
