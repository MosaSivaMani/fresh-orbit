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

export const isProduction = process.env.NODE_ENV === "production";
