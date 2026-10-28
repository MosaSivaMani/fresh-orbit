import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { type PublicCity } from "@/api/types/locations";

const { getCities, publicHttp } = vi.hoisted(() => ({
  getCities: vi.fn<() => Promise<PublicCity[]>>(),
  publicHttp: vi.fn((revalidateSeconds: number) => ({ revalidateSeconds })),
}));

vi.mock("@/api/endpoints/catalog", () => ({ getCities }));
vi.mock("@/api/server", () => ({ publicHttp }));

const { SiteFooter, FOOTER_CITIES_REVALIDATE_SECONDS } = await import("./site-footer");

describe("SiteFooter", () => {
  beforeEach(() => {
    getCities.mockReset();
    publicHttp.mockClear();
  });

  it("lists the cities the API serves, cached for at most five minutes (E2E-6)", async () => {
    getCities.mockResolvedValue([{ id: "c1", name: "Visakhapatnam" } as PublicCity]);

    render(await SiteFooter());

    expect(screen.getByText("Visakhapatnam")).toBeInTheDocument();
    expect(publicHttp).toHaveBeenCalledWith(FOOTER_CITIES_REVALIDATE_SECONDS);
    expect(FOOTER_CITIES_REVALIDATE_SECONDS).toBeLessThanOrEqual(300);
  });

  it("regenerates static pages as often as the city list expires", () => {
    // The root layout's `revalidate` must be a literal for Next.js, so it cannot import the constant.
    const layout = readFileSync(resolve(process.cwd(), "src/app/layout.tsx"), "utf8");

    expect(layout).toMatch(
      new RegExp(`export const revalidate = ${FOOTER_CITIES_REVALIDATE_SECONDS};`),
    );
  });

  it("links every policy page, privacy and terms included", async () => {
    getCities.mockResolvedValue([]);

    render(await SiteFooter());

    for (const href of [
      "/support/delivery-policy",
      "/support/cancellation-policy",
      "/support/refund-policy",
      "/support/privacy-policy",
      "/support/terms",
    ]) {
      expect(document.querySelector(`footer a[href="${href}"]`)).not.toBeNull();
    }
  });

  it("still renders, with the pincode hint, when the API is unavailable", async () => {
    getCities.mockRejectedValue(new Error("fetch failed"));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    render(await SiteFooter());

    expect(screen.getByText("Enter your pincode to check delivery.")).toBeInTheDocument();
    warn.mockRestore();
  });
});
