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
