import { Brush, Script } from "@fresh/ui";
import { Fish, Scale, Truck } from "lucide-react";
import Image from "next/image";
import { type ReactNode } from "react";
import { Birds, HarbourSky, HeroWaves, Sparks, Swoosh, Trawler } from "./scenery";

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

function FeatureChip({
  icon,
  title,
  line,
  detail,
}: {
  icon: ReactNode;
  title: string;
  line: string;
  detail: string;
}) {
  return (
    <li className="flex min-w-[10.5rem] items-center gap-3 text-left">
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
        <span className="text-text-muted text-xs">{detail}</span>
      </span>
    </li>
  );
}

/**
 * The home hero after the owner's reference (D188): a warm sunset harbour (drawn, or the optional background photo),
 * Padma with the catch on the left, "Fresh fish. / Cleaned your way. / At your door, in your slot." in the middle,
 * three feature chips, and "From today's catch to your kitchen" with a trawler on the right. The copy is fixed
 * marketing text; the only live value is the server's next slot (rule 4). Phones stack the text above Padma.
 */
export function HomeHero({ nextSlot = null, heroBackground = null }: HomeHeroProps) {
  return (
    <section
      aria-labelledby="home-hero-heading"
      className="relative isolate overflow-hidden bg-[linear-gradient(180deg,#F8C6A0_0%,#FBDDBE_42%,#FFF3E4_100%)] lg:h-[460px]"
    >
      {heroBackground ? (
        <Image
          src={heroBackground}
          alt=""
          fill
          priority
          sizes="100vw"
          className="-z-10 object-cover"
        />
      ) : (
        <HarbourSky className="-z-10" />
      )}

      <Birds className="text-outline-ink/45 absolute top-6 left-[46%] hidden w-28 md:block" />
      <Birds className="text-outline-ink/40 absolute top-24 right-[4%] w-20 lg:w-24" />
      <Trawler className="absolute right-[1%] bottom-6 hidden w-[200px] md:block lg:bottom-10 lg:w-[230px]" />

      <div className="relative mx-auto flex h-full max-w-7xl flex-col items-center px-4 pt-8 lg:block lg:px-8 lg:pt-10">
        <div className="flex flex-col items-center gap-6 text-center lg:mr-[8%] lg:ml-[36%] lg:items-start lg:text-left">
          <h1
            id="home-hero-heading"
            className="text-text flex flex-col items-center lg:items-start"
          >
            <span className="inline-flex items-start text-[clamp(2.875rem,5.6vw,4.75rem)]">
              <Script className="leading-[0.95]">Fresh fish.</Script>
              <Sparks className="text-accent-coral -mt-1 ml-1 w-9 lg:w-11" />
            </span>{" "}
            <Brush className="-mt-1 text-[clamp(1.75rem,3.2vw,2.625rem)] [--fo-brush-colour:#F5D04A]">
              <Script>Cleaned your way.</Script>
            </Brush>{" "}
            <span className="font-display mt-3 text-[clamp(1.75rem,3.2vw,2.625rem)] leading-[1.05] font-extrabold text-balance">
              At your door, in your slot.
            </span>
          </h1>

          <ul
            aria-label="Why Fresh Orbitt"
            className="flex flex-wrap justify-center gap-x-6 gap-y-4 lg:justify-start lg:gap-x-5"
          >
            <FeatureChip
              icon={<Fish />}
              title="CLEANED"
              line="Your way"
              detail="Whole · Steaks · Curry cut"
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
          </ul>
        </div>

        <p className="text-accent-coral-text font-script mt-6 -rotate-[8deg] text-center text-[1.625rem] leading-[1.05] font-bold lg:absolute lg:top-10 lg:right-8 lg:mt-0 lg:text-left lg:text-[2.125rem]">
          From today&apos;s catch
          <br />
          to your kitchen
          <Swoosh className="text-accent-coral mt-1 h-3 w-40 lg:w-52" />
        </p>

        <div className="relative mt-4 aspect-[5/4] w-full max-w-[440px] lg:absolute lg:bottom-0 lg:left-[-3%] lg:mt-0 lg:aspect-[3/2] lg:w-[46%] lg:max-w-[700px]">
          <Image
            src="/home/hero-catch.png"
            alt="Padma smiling and holding a large fresh seer fish over prawns, crabs and fish on ice"
            fill
            priority
            sizes="(min-width: 1024px) 46vw, 440px"
            className="object-cover object-[78%_0%] lg:object-contain lg:object-bottom"
          />
        </div>
      </div>

      <HeroWaves />
    </section>
  );
}
