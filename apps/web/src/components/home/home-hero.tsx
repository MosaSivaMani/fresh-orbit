import { FeatureChip, HarbourHero, HeroHeadline } from "@fresh/ui";
import { Fish, Scale, Truck } from "lucide-react";
import Image from "next/image";

export interface HarbourPicture {
  /** The full-size file. */
  src: string;
  /** Width-described sources (`"/a-1280.jpg 1280w, /a.jpg 2120w"`); the browser picks one for the screen. */
  srcSet?: string;
  alt: string;
}

export interface HomeHeroProps {
  /**
   * The server's next delivery slot, already formatted ("Sat 25 Oct, 7:00 am – 9:00 am"). Without it the delivery
   * chip says "Pick a slot at checkout".
   */
  nextSlot?: string | null;
  /**
   * The owner's harbour picture (slot `WEB_HOME_HERO_HARBOUR`, D279) once it is FINAL. It already shows Padma, the
   * gulls and the trawler, so it replaces the drawn scenery and the separate Padma picture. Null draws them instead.
   */
  harbour?: HarbourPicture | null;
}

// The picture's box is 4:3 on phones and 2:1 from `sm` (focus on Padma at the left), the whole hero from `lg`. Its
// rendered width is the box height times 20/7, so a phone needs about 2.1 times the screen width.
const HARBOUR_SIZES = "(min-width: 1024px) 100vw, (min-width: 640px) 143vw, 215vw";

/**
 * The home hero after the owner's reference (D188), built from the shared `HarbourHero` parts in `@fresh/ui`: Padma
 * with the catch, "Fresh fish. / Cleaned your way. / At your door, in your slot.", three feature chips and "From
 * today's catch to your kitchen". With the owner's harbour picture (D279) the text sits over its calm sky; without it
 * the scenery is drawn around the Padma picture. The copy is fixed marketing text; the only live value is the
 * server's next slot (rule 4).
 */
export function HomeHero({ nextSlot = null, harbour = null }: HomeHeroProps) {
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
      scene={
        harbour ? (
          <picture className="block size-full">
            <img
              src={harbour.src}
              srcSet={harbour.srcSet}
              sizes={HARBOUR_SIZES}
              alt={harbour.alt}
              fetchPriority="high"
              className="size-full object-cover object-[12%_50%] lg:object-[30%_70%]"
            />
          </picture>
        ) : null
      }
      picture={
        harbour ? null : (
          <Image
            src="/home/hero-catch.png"
            alt="Padma smiling and holding a large fresh seer fish over prawns, crabs and fish on ice"
            fill
            priority
            sizes="(min-width: 1024px) 35vw, 440px"
            className="object-cover object-[78%_0%] lg:object-[82%_0%]"
          />
        )
      }
    />
  );
}
