import Link from "next/link";
import { getBusinessProfile, getCities } from "@/api/endpoints/catalog";
import { publicHttp } from "@/api/server";
import { type PublicBusinessProfile, type PublicCity } from "@/api/types/locations";
import { getGrievanceOfficer } from "@/server/config";
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

/** The seller's legal identity (`GET /business-profile`, L-6, D300); null when the API is unavailable. */
async function loadProfile(): Promise<PublicBusinessProfile | null> {
  try {
    return await getBusinessProfile(publicHttp(FOOTER_CITIES_REVALIDATE_SECONDS));
  } catch (error) {
    console.warn(
      "Footer: business profile unavailable.",
      error instanceof Error ? error.message : error,
    );
    return null;
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
  { href: "/support#contact", label: "Contact us" },
  { href: "/support/delivery-policy", label: "Delivery policy" },
  { href: "/support/cancellation-policy", label: "Cancellation policy" },
  { href: "/support/refund-policy", label: "Refund policy" },
  { href: "/support/privacy-policy", label: "Privacy policy" },
  { href: "/support/terms", label: "Terms of use" },
  { href: "/about", label: "About us" },
];

const grievanceLink = { href: "/support#grievance", label: "Grievance officer" };

/** A value the owner has filled in; blank and missing values are left out, never guessed. */
function filled(value: string | null | undefined): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

/**
 * The legal block (L-6, D300): legal name, registered address, GSTIN, FSSAI licence and contacts, exactly as the
 * business profile holds them. Each line appears only when the server sends it.
 */
function LegalDetails({ profile }: { profile: PublicBusinessProfile }) {
  const phone = profile.supportPhone.replace(/\s+/g, "");
  const rows: LegalRow[] = [
    { field: "legalName", label: "Legal name", value: profile.legalName },
    { field: "address", label: "Registered address", value: profile.address },
    { field: "gstin", label: "GSTIN", value: profile.gstin },
    { field: "fssaiLicenseNo", label: "FSSAI licence no.", value: profile.fssaiLicenseNo },
    { field: "supportPhone", label: "Phone", value: profile.supportPhone, href: `tel:${phone}` },
    {
      field: "supportEmail",
      label: "Email",
      value: profile.supportEmail,
      href: `mailto:${profile.supportEmail}`,
    },
  ];
  const shown = rows.filter((r) => filled(r.value));
  if (shown.length === 0) return null;
  return (
    <section aria-label="Legal details">
      <dl className="grid gap-x-6 gap-y-1 text-xs sm:grid-cols-2 lg:grid-cols-3">
        {shown.map((r) => (
          <div key={r.field} className="flex gap-1.5" data-field={r.field}>
            <dt className="opacity-70">{r.label}:</dt>
            <dd className="break-words opacity-90">
              {r.href ? <a href={r.href}>{r.value}</a> : r.value}
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

interface LegalRow {
  /** The business-profile field the line shows (stable, for tests). */
  field: keyof PublicBusinessProfile;
  label: string;
  value: string | null | undefined;
  href?: string;
}

/**
 * The dark footer (D95): logo, shop and help links, the cities we deliver to (from the API), the seller's legal
 * details (from the business profile, D300) and the small print.
 */
export async function SiteFooter() {
  const [cities, profile] = await Promise.all([loadCities(), loadProfile()]);
  // The grievance officer is linked only once configured (L-7, D300).
  const help = getGrievanceOfficer() ? [...helpLinks, grievanceLink] : helpLinks;
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
          {help.map((l) => (
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
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-4 lg:px-8">
          {profile && <LegalDetails profile={profile} />}
          <p className="text-xs opacity-80">
            © {new Date().getFullYear()} Fresh Orbitt. Prices include GST.
          </p>
        </div>
      </div>
    </footer>
  );
}
