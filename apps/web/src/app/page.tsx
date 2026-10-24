import { formatDayShort, formatTime } from "@fresh/core";
import { Brush, FilterChip, FoodHero, PromiseCard, Script } from "@fresh/ui";
import Link from "next/link";
import { Suspense } from "react";
import { getHome, getPromotions, getServiceability, listProducts } from "@/api/endpoints/catalog";
import { loadWithLocation, requestApi } from "@/api/server";
import { CategoryRail } from "@/components/catalog/category-rail";
import { OfferStrip } from "@/components/catalog/offer-strip";
import { ProductGrid, offersBySlug } from "@/components/catalog/product-card";
import { GetTheApp } from "@/components/get-the-app";
import { SearchBox } from "@/components/header/search-box";
import { LocationBanner } from "@/components/location-banner";
import { Container, Section, SectionError } from "@/components/section";
import { settle } from "@/lib/settle";
import { PincodeForm } from "@/location/pincode-form";

/**
 * Home in the food-app style (D95, web-food-app.html version 2): the coral hero with the headline, a pincode box
 * that asks the server about delivery and the search box, three promise cards and two tilted character photos;
 * then the round category row, "Top picks in <city>", offers, "Filter and pick" and the get-the-app band. Every
 * slot, offer, price and city shown is the server's.
 */
export default async function HomePage() {
  const api = await requestApi();
  const [home, offers, serviceability, more] = await Promise.all([
    settle(loadWithLocation(api, getHome)),
    settle(loadWithLocation(api, getPromotions)),
    api.pincode ? settle(getServiceability(api.anonymous, api.pincode)) : null,
    settle(loadWithLocation(api, (http) => listProducts(http, { pageSize: 8 }))),
  ]);

  const area = serviceability?.ok ? serviceability.value : null;
  const nextSlot = area?.nextSlot ?? null;
  const city = area?.serviceable ? (area.city?.name ?? null) : null;
  const pincodeRefused = home.ok && home.value.pincodeRefused;
  const slotText = nextSlot
    ? `${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)} – ${formatTime(nextSlot.endAt)}`
    : null;
  const banners = offers.ok ? offers.value.data : [];
  const offerLines = offersBySlug(banners);
  const freshness = slotText ? `Next slot ${slotText}` : undefined;

  return (
    <>
      <FoodHero
        leftSlot="ONBOARDING_CLEANED"
        leftFocus="50% 28%"
        rightSlot="ONBOARDING_DELIVERY"
        rightFocus="45% 25%"
      >
        <h1 className="font-display text-on-block-coral text-[clamp(2.125rem,4.4vw,3.25rem)] leading-[1.05] font-extrabold text-balance">
          Fresh fish.{" "}
          <Brush tone="paper">
            <Script>Cleaned your way.</Script>
          </Brush>
          <br />
          At your door, in your slot.
        </h1>
        <div className="grid w-full gap-2.5 text-left sm:grid-cols-[minmax(230px,280px)_1fr] sm:items-start">
          <PincodeForm variant="hero" />
          <Suspense
            fallback={<div className="bg-surface h-[52px] rounded-2xl" aria-hidden="true" />}
          >
            <SearchBox className="bg-surface rounded-2xl p-1.5 shadow-[0_6px_16px_rgb(0_0_0/0.12)] [&_input]:h-11 [&_input]:border-0 [&_input]:shadow-none" />
          </Suspense>
        </div>
        <ul className="grid w-full gap-3 sm:grid-cols-3 sm:gap-3.5">
          <li>
            <PromiseCard title="CLEANED" line="YOUR WAY" label="WHOLE · STEAKS · CURRY CUT" />
          </li>
          <li>
            <PromiseCard title="PAY FOR" line="NET WEIGHT" label="WEIGHED AFTER CLEANING" />
          </li>
          <li>
            <PromiseCard
              title="DELIVERY"
              line="IN YOUR SLOT"
              label={slotText ? `NEXT SLOT ${slotText.toUpperCase()}` : "PICK A SLOT AT CHECKOUT"}
            />
          </li>
        </ul>
      </FoodHero>

      <Container className="flex flex-col gap-8 pt-7 pb-4">
        <LocationBanner pincode={api.pincode} pincodeRefused={pincodeRefused} />

        {home.ok && home.value.data.featuredCategories.length > 0 && (
          <Section title="What's on your" script="mind?">
            <CategoryRail categories={home.value.data.featuredCategories} />
          </Section>
        )}

        <hr className="border-border" />

        <Section
          title={city ? "Top picks in" : "Top picks"}
          script={city ?? "today"}
          description={
            home.ok && home.value.data.locationRequired
              ? "Prices show once you set your pincode."
              : undefined
          }
          action={{ href: "/shop", label: "See all" }}
        >
          {home.ok ? (
            home.value.data.featuredProducts.length > 0 ? (
              <ProductGrid
                products={home.value.data.featuredProducts}
                priorityCount={2}
                offers={offerLines}
                {...(freshness ? { freshness } : {})}
              />
            ) : (
              <p className="text-text-muted">
                Nothing is featured right now.{" "}
                <Link href="/shop" className="text-accent-text font-semibold underline">
                  Browse the shop
                </Link>
                .
              </p>
            )
          ) : (
            <SectionError message={home.message} traceId={home.traceId} />
          )}
        </Section>

        {banners.length > 0 && (
          <Section id="offers" title="Today's" script="offers">
            <OfferStrip banners={banners} />
          </Section>
        )}

        <hr className="border-border" />

        <Section title="Filter and" script="pick" action={{ href: "/shop", label: "All seafood" }}>
          <ul className="flex flex-wrap gap-2" aria-label="Quick filters">
            {api.pincode && (
              <li>
                <FilterChip asChild>
                  <Link href="/shop?sort=price">Price: low to high</Link>
                </FilterChip>
              </li>
            )}
            <li>
              <FilterChip asChild>
                <Link href="/shop?source=WILD">Wild caught</Link>
              </FilterChip>
            </li>
            <li>
              <FilterChip asChild>
                <Link href="/shop?source=FARMED">Farmed</Link>
              </FilterChip>
            </li>
            <li>
              <FilterChip asChild>
                <Link href="/shop?inStock=true">In stock</Link>
              </FilterChip>
            </li>
            {api.pincode && (
              <li>
                <FilterChip asChild>
                  <Link href="/shop?max=500">Under ₹500</Link>
                </FilterChip>
              </li>
            )}
            <li>
              <FilterChip asChild>
                <Link href="/shop?sort=-newest">Newest</Link>
              </FilterChip>
            </li>
          </ul>
          {more.ok ? (
            <ProductGrid
              products={more.value.data.items}
              offers={offerLines}
              {...(freshness ? { freshness } : {})}
            />
          ) : (
            <SectionError message={more.message} traceId={more.traceId} />
          )}
        </Section>

        <GetTheApp />
      </Container>
    </>
  );
}
