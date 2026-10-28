import { formatDayShort, formatTime } from "@fresh/core";
import { illustrations } from "@fresh/illustrations";
import { FilterChip, WaveBand } from "@fresh/ui";
import Link from "next/link";
import { getHome, getPromotions, getServiceability, listProducts } from "@/api/endpoints/catalog";
import { loadWithLocation, requestApi } from "@/api/server";
import { OfferStrip } from "@/components/catalog/offer-strip";
import { ProductGrid, offersBySlug } from "@/components/catalog/product-card";
import { GetTheApp } from "@/components/get-the-app";
import { HomeCategories } from "@/components/home/home-categories";
import { HomeCheckBar } from "@/components/home/home-check-bar";
import { HomeHero } from "@/components/home/home-hero";
import { PromoCards } from "@/components/home/promo-cards";
import { LocationBanner } from "@/components/location-banner";
import { Container, Section, SectionError } from "@/components/section";
import { settle } from "@/lib/settle";

/** A 1280 px copy of the owner's harbour picture for phones and laptops (D279). */
const HARBOUR_1280 = "/home/web_home_hero_harbour-1280.jpg";

/**
 * Home after the owner's reference (D188, `docs/design/mockups/web-home-reference-2026-10-28.png`): the harbour hero
 * with Padma, the headline and three feature chips; the white pincode-check and search bar over its bottom edge;
 * "What's on your mind?" with the server's featured categories; a pale wave band with three promo cards; then "Top
 * picks in <city>", the location's bestsellers (CAT-R24; left out when the server has none), offers, "Filter and
 * pick" and the get-the-app band. Every slot, offer, price, ranking, category and city shown is the server's.
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
  // Slot times read in the store's time zone (D258).
  const zone = area?.city?.timeZone;
  const slotText = nextSlot
    ? `${formatDayShort(nextSlot.startAt, zone)}, ${formatTime(nextSlot.startAt, zone)} – ${formatTime(nextSlot.endAt, zone)}`
    : null;
  const banners = offers.ok ? offers.value.data : [];
  const offerLines = offersBySlug(banners);
  const freshness = slotText ? `Next slot ${slotText}` : undefined;
  const bestsellers = home.ok ? (home.value.data.bestsellers ?? []) : [];
  // The owner's harbour picture (FINAL since D279) replaces the drawn scenery and the separate Padma picture. The
  // 1280 px copy in public/home serves smaller screens (next/image does not resize here: `unoptimized`).
  const harbourSlot = illustrations.WEB_HOME_HERO_HARBOUR;
  const harbourSrc = `/illustrations/${harbourSlot.src}`;
  const harbour =
    harbourSlot.status === "FINAL"
      ? {
          src: harbourSrc,
          srcSet: `${HARBOUR_1280} 1280w, ${harbourSrc} ${harbourSlot.width}w`,
          alt: harbourSlot.alt,
        }
      : null;

  return (
    <>
      <HomeHero nextSlot={slotText} harbour={harbour} />
      <HomeCheckBar />

      <Container className="flex flex-col gap-8 pt-10 pb-8">
        <LocationBanner pincode={api.pincode} pincodeRefused={pincodeRefused} />

        {home.ok && <HomeCategories categories={home.value.data.featuredCategories} />}
      </Container>

      <WaveBand>
        <Container>
          <PromoCards />
        </Container>
      </WaveBand>

      <Container className="flex flex-col gap-8 pt-8 pb-4">
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

        {bestsellers.length > 0 && (
          <Section
            id="bestsellers"
            title={city ? "Bestsellers in" : "Best"}
            script={city ?? "sellers"}
          >
            <ProductGrid
              products={bestsellers}
              offers={offerLines}
              {...(freshness ? { freshness } : {})}
            />
          </Section>
        )}

        {banners.length > 0 && (
          <Section id="offers" title="Today's" script="offers">
            <OfferStrip
              banners={banners}
              categorySlugs={
                home.ok
                  ? Object.fromEntries(
                      home.value.data.featuredCategories.flatMap((c) => [
                        [c.id, c.slug],
                        ...c.children.map((child) => [child.id, child.slug]),
                      ]),
                    )
                  : {}
              }
            />
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
