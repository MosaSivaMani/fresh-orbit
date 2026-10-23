import { formatDayShort, formatTime } from "@fresh/core";
import Link from "next/link";
import { getHome, getPromotions, getServiceability } from "@/api/endpoints/catalog";
import { loadWithLocation, requestApi } from "@/api/server";
import { CategoryRail } from "@/components/catalog/category-rail";
import { OfferStrip } from "@/components/catalog/offer-strip";
import { ProductGrid } from "@/components/catalog/product-card";
import { ChangePincodeButton } from "@/components/change-pincode-button";
import { HeroArt } from "@/components/hero-art";
import { LocationBanner } from "@/components/location-banner";
import { Container, Section, SectionError } from "@/components/section";
import { TrustRow } from "@/components/trust-row";
import { settle } from "@/lib/settle";

export default async function HomePage() {
  const api = await requestApi();
  const [home, offers, serviceability] = await Promise.all([
    settle(loadWithLocation(api, getHome)),
    settle(loadWithLocation(api, getPromotions)),
    api.pincode ? settle(getServiceability(api.anonymous, api.pincode)) : null,
  ]);

  const nextSlot = serviceability?.ok ? serviceability.value.nextSlot : null;
  const pincodeRefused = home.ok && home.value.pincodeRefused;

  return (
    <div className="flex flex-col gap-14 pb-4">
      <section aria-labelledby="hero-heading" className="bg-primary text-on-primary">
        <Container className="grid items-center gap-8 py-12 md:grid-cols-2 md:py-16">
          <div className="flex flex-col gap-5">
            <h1 id="hero-heading" className="text-4xl leading-tight font-bold sm:text-5xl">
              Fresh seafood, cleaned the way you cook it.
            </h1>
            <p className="text-lg opacity-90">
              {nextSlot
                ? `Next delivery slot for ${api.pincode}: ${formatDayShort(nextSlot.startAt)}, ${formatTime(nextSlot.startAt)} – ${formatTime(nextSlot.endAt)}.`
                : api.pincode
                  ? "Choose fish, prawns and crabs, and the cut you want. You pay for net weight after cleaning."
                  : "Tell us your pincode to see prices, stock and delivery slots for your area."}
            </p>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/shop"
                className="bg-highlight text-on-highlight hover:bg-highlight-hover inline-flex h-12 items-center rounded-md px-6 text-base font-semibold"
              >
                Shop the catch
              </Link>
              <ChangePincodeButton
                label={api.pincode ? `Deliver to ${api.pincode}` : "Set pincode"}
                variant="outline"
                size="lg"
              />
            </div>
          </div>
          <HeroArt />
        </Container>
      </section>

      <Container className="flex flex-col gap-14">
        <LocationBanner pincode={api.pincode} pincodeRefused={pincodeRefused} />

        {home.ok && home.value.data.featuredCategories.length > 0 && (
          <Section title="Shop by category" action={{ href: "/shop", label: "See all" }}>
            <CategoryRail categories={home.value.data.featuredCategories} />
          </Section>
        )}

        {offers.ok && offers.value.data.length > 0 && (
          <Section title="Offers">
            <OfferStrip banners={offers.value.data} />
          </Section>
        )}

        <Section
          title="Featured today"
          description={
            home.ok && home.value.data.locationRequired
              ? "Prices show once you set your pincode."
              : undefined
          }
          action={{ href: "/shop", label: "Shop all" }}
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

        <Section title="Why shop with us">
          <TrustRow />
        </Section>
      </Container>
    </div>
  );
}
