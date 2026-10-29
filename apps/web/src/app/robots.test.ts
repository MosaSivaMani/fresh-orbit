import { afterEach, describe, expect, it, vi } from "vitest";
import { isIndexable } from "@/server/config";
import robots from "./robots";

function deployment(appEnvironment: string | undefined, siteUrl = "https://example.in") {
  vi.stubEnv("NODE_ENV", "production");
  vi.stubEnv("NEXT_PUBLIC_SITE_URL", siteUrl);
  vi.stubEnv("APP_ENVIRONMENT", appEnvironment);
}

describe("robots (L-45)", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("lets crawlers in, with the sitemap, only on the Production deployment", () => {
    deployment("Production");

    const result = robots();

    expect(isIndexable()).toBe(true);
    expect(result.rules).toMatchObject({ userAgent: "*", allow: "/" });
    expect(result.sitemap).toBe("https://example.in/sitemap.xml");
  });

  it("asks every crawler away on staging, the demo, tests or an unset environment", () => {
    for (const appEnvironment of ["Staging", "Demo", "Testing", "production", undefined]) {
      deployment(appEnvironment);

      expect(isIndexable()).toBe(false);
      expect(robots()).toEqual({ rules: { userAgent: "*", disallow: "/" } });
    }
  });

  it("is never indexable without a public site URL or outside a production build", () => {
    deployment("Production", "");
    expect(isIndexable()).toBe(false);

    deployment("Production");
    vi.stubEnv("NODE_ENV", "development");
    expect(isIndexable()).toBe(false);
    expect(robots()).toEqual({ rules: { userAgent: "*", disallow: "/" } });
  });
});
