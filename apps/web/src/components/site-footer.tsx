import Link from "next/link";
import { getCities } from "@/api/endpoints/catalog";
import { publicHttp } from "@/api/server";
import { type PublicCity } from "@/api/types/locations";
import { Logo } from "./logo";

/**
 * How long the footer's city list may be served from cache (E2E-6): short, so a city the staff activate appears
 * within minutes, like the category list. Keep the root layout's `revalidate` (static pages) equal to it.
 */
export const FOOTER_CITIES_REVALIDATE_SECONDS = 300;

async function loadCities(): Promise<PublicCity[]> {
  try {
    return await getCities(publicHttp(FOOTER_CITIES_REVALIDATE_SECONDS));
  } catch (error) {
    // The footer must render even when the API is down or not configured (e.g. at build time).
    console.warn("Footer: cities unavailable.", error instanceof Error ? error.message : error);
    return [];
  }
}

const shopLinks = [
  { href: "/shop", label: "All seafood" },
  { href: "/search", label: "Search" },
  { href: "/cart", label: "Cart" },
  { href: "/account/orders", label: "Your orders" },
];

const helpLinks = [
  { href: "/support", label: "Help and FAQ" },
  { href: "/support/delivery-policy", label: "Delivery policy" },
  { href: "/support/cancellation-policy", label: "Cancellation policy" },
  { href: "/support/refund-policy", label: "Refund policy" },
  { href: "/about", label: "About us" },
];

/** The dark footer (D95): logo, shop and help links, the cities we deliver to (from the API) and the small print. */
export async function SiteFooter() {
  const cities = await loadCities();
  return (
    <footer className="bg-outline-ink text-on-outline-ink mt-12">
      <div className="mx-auto grid max-w-7xl gap-10 px-4 py-12 sm:grid-cols-2 lg:grid-cols-4 lg:px-8">
        <div className="flex flex-col gap-3">
          <Logo tone="inverse" />
          <p className="text-sm opacity-90">
            Fresh fish and seafood, cleaned and cut to order, delivered from our own store.
          </p>
        </div>
        <nav aria-label="Shop" className="flex flex-col gap-2 text-sm">
          <h2 className="font-display text-base font-semibold">Shop</h2>
          {shopLinks.map((l) => (
            <Link key={l.href} href={l.href} className="w-fit underline-offset-4 hover:underline">
              {l.label}
            </Link>
          ))}
        </nav>
        <nav aria-label="Help" className="flex flex-col gap-2 text-sm">
          <h2 className="font-display text-base font-semibold">Help</h2>
          {helpLinks.map((l) => (
            <Link key={l.href} href={l.href} className="w-fit underline-offset-4 hover:underline">
              {l.label}
            </Link>
          ))}
        </nav>
        <div className="flex flex-col gap-2 text-sm">
          <h2 className="font-display text-base font-semibold">Cities we serve</h2>
          {cities.length > 0 ? (
            <ul className="flex flex-col gap-1">
              {cities.map((city) => (
                <li key={city.id}>{city.name}</li>
              ))}
            </ul>
          ) : (
            <p className="opacity-90">Enter your pincode to check delivery.</p>
          )}
        </div>
      </div>
      <div className="border-on-outline-ink/20 border-t">
        <p className="mx-auto max-w-7xl px-4 py-4 text-xs opacity-80 lg:px-8">
          © {new Date().getFullYear()} Fresh Orbitt. Prices include GST.
        </p>
      </div>
    </footer>
  );
}
