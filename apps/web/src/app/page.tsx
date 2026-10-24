import { formatDayShort, formatTime } from "@fresh/core";
import { AvatarCircle, BentoTile, Button, Heading, Script, Sticker } from "@fresh/ui";
import { ChevronRight } from "lucide-react";
import Link from "next/link";
import { getHome, getPromotions, getServiceability } from "@/api/endpoints/catalog";
import { loadWithLocation, requestApi } from "@/api/server";
import { CategoryRail } from "@/components/catalog/category-rail";
import { OfferStrip } from "@/components/catalog/offer-strip";
import { ProductGrid } from "@/components/catalog/product-card";
import { ChangePincodeButton } from "@/components/change-pincode-button";
import { LocationBanner } from "@/components/location-banner";
import { Container, Section, SectionError } from "@/components/section";
import { settle } from "@/lib/settle";

/**
 * Home (D88, web mockup): a wide coral hero block with the fishmonger in a circle avatar, a three-tile bento of
 * store policies, category bubbles, offers and the featured grid. Every number, slot and offer is the server's.
 */
export default async function HomePage() {
  const api = await requestApi();
  const [home, offers, serviceability] = await Promise.all([
    settle(loadWithLocation(api, getHome)),
    settle(loadWithLocation(api, getPromotions)),
    api.pincode ? settle(getServiceability(api.anonymous, api.pincode)) : null,
  ]);

  const nextSlot = serviceability?.ok ? serviceability.value.nextSlot : null;
  const pincodeRefused = home.ok && home.value.pincodeRefused;
  const firstOffer = offers.ok ? offers.value.data[0] : undefined;

  return (
    <div className="flex flex-col gap-12 pb-8">
      <Container className="pt-4 sm:pt-6">
        <section
          aria-labelledby="hero-heading"
          className="bg-block-coral text-on-block-coral grid items-center gap-6 overflow-hidden rounded-[32px] px-6 py-8 sm:px-10 sm:py-10 md:grid-cols-2"
        >
          <div className="flex flex-col items-start gap-4">
            <p className="text-xs font-bold tracking-[0.12em] uppercase">
              Fresh fish and seafood, cleaned to order
            </p>
            <Heading
              id="hero-heading"
              level={1}
              size="display"
              line1="Straight off"
              line2="the boats"
              script="line1"
              brush="line2"
              doodle
              className="lg:text-[4rem]"
            />
            <p className="max-w-[44ch] text-base sm:text-lg">
              {nextSlot
                ? `Next delivery slot for ${api.pincode}: ${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)} – ${formatTime(nextSlot.endAt)}.`
                : api.pincode
                  ? "Choose fish, prawns and crabs, and the cut you want. You pay for net weight after cleaning."
                  : "Tell us your pincode to see prices, stock and delivery slots for your area."}
            </p>
            <div className="flex flex-wrap gap-3">
              <Button
                asChild
                size="lg"
                className="bg-outline-ink text-on-outline-ink hover:bg-outline-ink rounded-2xl hover:opacity-90"
              >
                <Link href="/shop">
                  See today&apos;s catch <ChevronRight aria-hidden="true" />
                </Link>
              </Button>
              <ChangePincodeButton
                label={api.pincode ? `Deliver to ${api.pincode}` : "Set pincode"}
                variant="outline"
                size="lg"
                className="border-on-block-coral text-on-block-coral rounded-2xl border-2 bg-transparent hover:bg-transparent"
              />
            </div>
          </div>
          <div className="relative grid min-h-[220px] place-items-center sm:min-h-[300px]">
            <AvatarCircle
              slot="ONBOARDING_FRESH"
              size="lg"
              focus="55% 22%"
              priority
              decorative={false}
              className="sm:size-[300px]"
            />
            <Sticker tone="mango" size="sm" className="absolute top-[10%] left-[4%]">
              Cleaned to order
            </Sticker>
            <Sticker tone="teal" size="sm" tilt="right" className="absolute right-[2%] bottom-[14%]">
              Pay on delivery
            </Sticker>
          </div>
        </section>

        <ul className="mt-4 grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-3">
          <li className="col-span-2 md:col-span-1">
            <BentoTile
              asChild
              wide
              tone="teal"
              className="h-full md:col-span-1"
              title={
                <>
                  Cleaned <Script className="text-mango">your way</Script>
                </>
              }
              description="Whole, steaks, curry cut or fillet"
              slot="ONBOARDING_CLEANED"
              slotFocus="50% 28%"
            >
              <Link href="/shop" aria-label="Cleaned your way: shop by cut" />
            </BentoTile>
          </li>
          <li>
            <BentoTile
              tone="mango"
              className="h-full"
              title={
                <>
                  Pick your <Script>slot</Script>
                </>
              }
              description={
                nextSlot
                  ? `Next: ${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)}`
                  : "Choose a delivery window at checkout"
              }
              slot="ONBOARDING_DELIVERY"
              slotFocus="45% 28%"
            />
          </li>
          <li>
            {firstOffer ? (
              <BentoTile
                tone="ice"
                className="h-full"
                chip="Offer"
                title={firstOffer.title}
                description={firstOffer.subtitle ?? undefined}
                slot="EMPTY_CART"
                slotFocus="50% 45%"
              />
            ) : (
              <BentoTile
                tone="ice"
                className="h-full"
                title={
                  <>
                    Pay for <Script>net weight</Script>
                  </>
                }
                description="Weighed after cleaning"
                slot="FEATURE_HONEST_WEIGHT"
              />
            )}
          </li>
        </ul>
      </Container>

      <Container className="flex flex-col gap-12">
        <LocationBanner pincode={api.pincode} pincodeRefused={pincodeRefused} />

        {home.ok && home.value.data.featuredCategories.length > 0 && (
          <Section title="Shop by" script="category" action={{ href: "/shop", label: "See all" }}>
            <CategoryRail categories={home.value.data.featuredCategories} />
          </Section>
        )}

        {offers.ok && offers.value.data.length > 0 && (
          <Section id="offers" title="Today's" script="offers">
            <OfferStrip banners={offers.value.data} />
          </Section>
        )}

        <Section
          title="Featured"
          script="today"
          description={
            home.ok && home.value.data.locationRequired
              ? "Prices show once you set your pincode."
              : undefined
          }
          action={{ href: "/shop", label: "See all" }}
        >
          {home.ok ? (
            home.value.data.featuredProducts.length > 0 ? (
              <ProductGrid products={home.value.data.featuredProducts} priorityCount={2} />
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
      </Container>
    </div>
  );
}
