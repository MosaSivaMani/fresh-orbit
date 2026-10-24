import { formatDayShort, formatTime } from "@fresh/core";
import { BentoTile, Heading, Script } from "@fresh/ui";
import Link from "next/link";
import { getHome, getPromotions, getServiceability } from "@/api/endpoints/catalog";
import { loadWithLocation, requestApi } from "@/api/server";
import { CategoryRail } from "@/components/catalog/category-rail";
import { OfferStrip } from "@/components/catalog/offer-strip";
import { ProductGrid } from "@/components/catalog/product-card";
import { PageSearch } from "@/components/header/page-search";
import { LocationBanner } from "@/components/location-banner";
import { Container, Section, SectionError } from "@/components/section";
import { settle } from "@/lib/settle";

/**
 * Home: the app home, widened (D88, web mockup). Greeting heading, search, a bento with a wide teal tile and coral
 * and mango tiles, categories, offers and the product grid; the floating cart bar comes from the layout. Every slot,
 * offer and price shown is the server's.
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
  const slotText = nextSlot
    ? `${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)} – ${formatTime(nextSlot.endAt)}`
    : null;

  return (
    <Container className="flex flex-col gap-10 pt-4 pb-10 sm:pt-6">
      <div className="flex flex-col gap-3">
        <div className="flex flex-col gap-0.5">
          <p className="text-text-muted text-sm">
            {api.pincode
              ? `Delivering to ${api.pincode}`
              : "Fresh fish and seafood, cleaned to order"}
          </p>
          <Heading level={1} size="page" className="text-text sm:text-[2.5rem]">
            What&apos;s cooking <Script tone="accent">today?</Script>
          </Heading>
        </div>
        <PageSearch />
      </div>

      <ul className="grid grid-cols-2 gap-3 md:grid-cols-[2fr_1fr_1fr]">
        <li className="col-span-2 md:col-span-1">
          <BentoTile
            asChild
            wide
            tone="teal"
            chip={slotText ? `Next slot ${slotText}` : undefined}
            title="Shop the catch"
            description="Fish, prawns, crabs and more"
            slot="ONBOARDING_FRESH"
            slotFocus="55% 22%"
            className="h-full md:min-h-[170px] md:[&_h3]:text-3xl"
          >
            <Link href="/shop" aria-label="Shop the catch" />
          </BentoTile>
        </li>
        <li>
          <BentoTile
            tone="coral"
            title="Cleaned your way"
            description="Whole, steaks, curry cut or fillet"
            slot="ONBOARDING_CLEANED"
            slotFocus="50% 28%"
            className="h-full md:min-h-[170px]"
          />
        </li>
        <li>
          <BentoTile
            tone="mango"
            title="Pick your slot"
            description={slotText ? `Next: ${slotText}` : "Choose a delivery window at checkout"}
            slot="ONBOARDING_DELIVERY"
            slotFocus="45% 28%"
            className="h-full md:min-h-[170px]"
          />
        </li>
      </ul>

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
  );
}
