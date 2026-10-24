import {
  AppBand,
  Brush,
  CategoryCircle,
  CharacterState,
  FilterChip,
  FoodCard,
  FoodHero,
  PageBand,
  PriceTag,
  PromiseCard,
  Script,
} from "@fresh/ui";
import { Fish } from "lucide-react";
import { useState } from "react";

/** The website's food-app components (D95). Sample values only; on the site every value is the server's. */
export function FoodAppPage() {
  const [qty, setQty] = useState(0);
  return (
    <div className="flex flex-col gap-10">
      <section className="flex flex-col gap-3">
        <h2 className="font-display text-xl font-bold">Hero</h2>
        <div className="overflow-hidden rounded-3xl">
          <FoodHero leftSlot="ONBOARDING_CLEANED" rightSlot="ONBOARDING_DELIVERY">
            <p className="font-display text-on-block-coral text-4xl leading-[1.05] font-extrabold">
              Fresh fish.{" "}
              <Brush tone="paper">
                <Script>Cleaned your way.</Script>
              </Brush>
            </p>
            <ul className="grid w-full gap-3 sm:grid-cols-3">
              <li>
                <PromiseCard title="CLEANED" line="YOUR WAY" label="WHOLE · STEAKS" />
              </li>
              <li>
                <PromiseCard title="PAY FOR" line="NET WEIGHT" label="WEIGHED AFTER CLEANING" />
              </li>
              <li>
                <PromiseCard title="DELIVERY" line="IN YOUR SLOT" label="NEXT SLOT TODAY 4 PM" />
              </li>
            </ul>
          </FoodHero>
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-xl font-bold">Category circles</h2>
        <div className="flex flex-wrap gap-4">
          {["Fish", "Prawns", "Crab", "Squid"].map((name, i) => (
            <CategoryCircle
              key={name}
              href="#"
              label={name}
              selected={i === 1}
              icon={<Fish className="text-accent-text" />}
            />
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-xl font-bold">Picture cards and chips</h2>
        <div className="flex flex-wrap gap-2">
          <FilterChip>Wild caught</FilterChip>
          <FilterChip pressed>In stock</FilterChip>
        </div>
        <ul className="grid grid-cols-1 gap-6 min-[480px]:grid-cols-2 lg:grid-cols-4">
          <li>
            <FoodCard
              name="Seer fish steaks"
              label="Seer fish steaks"
              href="#"
              pictureTone="ice"
              picture={<Fish className="text-accent-text size-16" strokeWidth={1.5} />}
              offer="₹90 OFF"
              freshness="Next slot today, 4 pm"
              meta="Vanjaram"
              price={<PriceTag paise={59000} mrpPaise={68000} per="/ kg" size="sm" />}
              quantity={qty}
              min={1}
              max={5}
              onAdd={() => setQty(1)}
              onQuantityChange={setQty}
            />
          </li>
          <li>
            <FoodCard
              name="Silver pomfret"
              label="Silver pomfret"
              href="#"
              pictureTone="teal"
              picture={<Fish className="text-accent-text size-16" strokeWidth={1.5} />}
              freshness="Next slot today, 4 pm"
              price={<PriceTag paise={42000} per="/ piece" size="sm" />}
              addHref="#"
            />
          </li>
          <li>
            <FoodCard
              name="Mud crab"
              label="Mud crab"
              href="#"
              pictureTone="mango"
              picture={<Fish className="text-accent-text size-16" strokeWidth={1.5} />}
              price={<PriceTag paise={88000} per="/ kg" size="sm" />}
              unavailableLabel="Out of stock"
            />
          </li>
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-xl font-bold">Page band, state and app band</h2>
        <div className="overflow-hidden rounded-3xl">
          <PageBand
            headingLevel={3}
            eyebrow="Home › Shop"
            title={
              <>
                All <Script>seafood</Script>
              </>
            }
            description="Fish, prawns, crabs and shellfish."
          />
        </div>
        <CharacterState
          headingLevel={3}
          slot="NO_SEARCH_RESULTS"
          title="No products match these filters"
          description="Try removing a filter."
        />
        <AppBand
          headingLevel={3}
          title={
            <>
              For a better experience, <Script tone="accent">get the app</Script>
            </>
          }
          description="Order, follow your delivery and reorder from your phone."
          fallback="Coming soon to Android and iPhone."
          slot="ONBOARDING_DELIVERY"
        />
      </section>
    </div>
  );
}
