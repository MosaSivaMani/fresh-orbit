// Server-side configuration. Missing configuration is refused, never guessed (CLAUDE.md rule 7).

export class ConfigurationMissingError extends Error {
  override readonly name = "ConfigurationMissingError";
  readonly code = "CONFIGURATION_MISSING";
  constructor(readonly variable: string) {
    super(`The environment variable ${variable} is not set; see apps/web/.env.example.`);
  }
}

/** Origin of FreshOrbit.Api, e.g. `http://localhost:5080` (no trailing slash, no `/api/v1`). */
export function getApiOrigin(): string {
  const value = process.env.FRESH_API_ORIGIN?.trim();
  if (!value) throw new ConfigurationMissingError("FRESH_API_ORIGIN");
  return value.replace(/\/+$/, "");
}

/** Public origin of this website, for canonical URLs, sitemap and JSON-LD; null when not configured. */
export function getSiteUrl(): string | null {
  const value = process.env.NEXT_PUBLIC_SITE_URL?.trim();
  return value ? value.replace(/\/+$/, "") : null;
}

/** Store pages of the customer app, for the "get the app" band; an unset one is simply not shown. */
export function getAppStoreLinks(): { label: string; href: string }[] {
  const links = [
    { label: "Google Play", href: process.env.FRESH_PLAY_STORE_URL?.trim() },
    { label: "App Store", href: process.env.FRESH_APP_STORE_URL?.trim() },
  ];
  return links.filter((l): l is { label: string; href: string } => Boolean(l.href));
}

export const isProduction = process.env.NODE_ENV === "production";

/**
 * The free public demo (render.yaml, docs/runbooks/render-demo.md): every page shows the "test data only" banner.
 * `NEXT_PUBLIC_DEMO_MODE=true`, inlined at build time; unset everywhere else.
 */
export function isDemoMode(): boolean {
  return process.env.NEXT_PUBLIC_DEMO_MODE?.trim() === "true";
}

/**
 * Whether search engines may index this deployment (L-45, D301). Only the production server is: Next's
 * `NODE_ENV=production` (every image), `APP_ENVIRONMENT=Production` (the value the API and Worker run with, from the
 * server's `.env.prod`; also a build argument, for prerendered pages) and a configured public URL. Staging, the demo,
 * a dev server, or an image run without `APP_ENVIRONMENT` is never indexed.
 */
export function isIndexable(): boolean {
  return (
    process.env.NODE_ENV === "production" &&
    process.env.APP_ENVIRONMENT?.trim() === "Production" &&
    getSiteUrl() !== null
  );
}

/** The grievance officer published on the help page (L-7, D300). */
export interface GrievanceOfficer {
  name: string;
  /** Optional, e.g. "Grievance Officer". */
  designation: string | null;
  email: string;
  /** How soon a complaint is answered, in the owner's words. */
  responseTime: string;
}

/**
 * The grievance officer from `FRESH_GRIEVANCE_OFFICER_NAME`, `FRESH_GRIEVANCE_OFFICER_EMAIL` and
 * `FRESH_GRIEVANCE_RESPONSE_TIME` (all three required), plus `FRESH_GRIEVANCE_OFFICER_DESIGNATION` (optional). The
 * business profile has no such fields yet (follow-up in docs/team/next-steps.md). Null, and nothing is shown, unless
 * all three required values are set: a partial officer is never published, never guessed.
 */
export function getGrievanceOfficer(): GrievanceOfficer | null {
  const name = process.env.FRESH_GRIEVANCE_OFFICER_NAME?.trim();
  const email = process.env.FRESH_GRIEVANCE_OFFICER_EMAIL?.trim();
  const responseTime = process.env.FRESH_GRIEVANCE_RESPONSE_TIME?.trim();
  if (!name || !email || !responseTime) {
    if (name || email || responseTime) {
      console.warn(
        "Grievance officer not shown: set FRESH_GRIEVANCE_OFFICER_NAME, FRESH_GRIEVANCE_OFFICER_EMAIL and FRESH_GRIEVANCE_RESPONSE_TIME.",
      );
    }
    return null;
  }
  const designation = process.env.FRESH_GRIEVANCE_OFFICER_DESIGNATION?.trim() || null;
  return { name, designation, email, responseTime };
}
