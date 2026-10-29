import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { type PublicBusinessProfile, type PublicCity } from "@/api/types/locations";

const { getCities, getBusinessProfile, publicHttp } = vi.hoisted(() => ({
  getCities: vi.fn<() => Promise<PublicCity[]>>(),
  getBusinessProfile: vi.fn<() => Promise<PublicBusinessProfile>>(),
  publicHttp: vi.fn((revalidateSeconds: number) => ({ revalidateSeconds })),
}));

vi.mock("@/api/endpoints/catalog", () => ({ getCities, getBusinessProfile }));
vi.mock("@/api/server", () => ({ publicHttp }));

const { SiteFooter, FOOTER_CITIES_REVALIDATE_SECONDS } = await import("./site-footer");

const fullProfile: PublicBusinessProfile = {
  legalName: "Example Seafoods Private Limited",
  tradeName: "Example",
  gstin: "37ABCDE1234F1Z5",
  fssaiLicenseNo: "12345678901234",
  address: "1 Harbour Road, Example Town 530001",
  supportPhone: "+919876543210",
  supportEmail: "help@example.in",
  supportHours: "7 AM to 9 PM",
  timeZone: "Asia/Kolkata",
};

/** The business-profile fields the footer's legal block shows, in order. */
function legalFields(): (string | null)[] {
  const rows = document.querySelectorAll("footer [data-field]");
  return Array.from(rows, (row) => row.getAttribute("data-field"));
}

describe("SiteFooter", () => {
  beforeEach(() => {
    getCities.mockReset();
    getBusinessProfile.mockReset();
    getBusinessProfile.mockResolvedValue(fullProfile);
    publicHttp.mockClear();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
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

  it("shows the seller's legal details exactly as the business profile sends them (L-6)", async () => {
    getCities.mockResolvedValue([]);

    render(await SiteFooter());

    expect(legalFields()).toEqual([
      "legalName",
      "address",
      "gstin",
      "fssaiLicenseNo",
      "supportPhone",
      "supportEmail",
    ]);
    expect(screen.getByText("Example Seafoods Private Limited")).toBeInTheDocument();
    expect(screen.getByText("1 Harbour Road, Example Town 530001")).toBeInTheDocument();
    expect(screen.getByText("37ABCDE1234F1Z5")).toBeInTheDocument();
    expect(screen.getByText("12345678901234")).toBeInTheDocument();
    expect(document.querySelector('footer a[href="tel:+919876543210"]')).not.toBeNull();
    expect(document.querySelector('footer a[href="mailto:help@example.in"]')).not.toBeNull();
  });

  it("leaves out every legal line the server does not send", async () => {
    getCities.mockResolvedValue([]);
    getBusinessProfile.mockResolvedValue({
      ...fullProfile,
      gstin: null,
      fssaiLicenseNo: null,
      address: "  ",
    });

    render(await SiteFooter());

    expect(legalFields()).toEqual(["legalName", "supportPhone", "supportEmail"]);
  });

  it("renders without a legal block when the business profile is unavailable", async () => {
    getCities.mockResolvedValue([]);
    getBusinessProfile.mockRejectedValue(new Error("fetch failed"));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    render(await SiteFooter());

    expect(legalFields()).toEqual([]);
    expect(document.querySelector('footer a[href="/support/terms"]')).not.toBeNull();
    warn.mockRestore();
  });

  it("links the contact section, and the grievance officer only once configured (L-7)", async () => {
    getCities.mockResolvedValue([]);

    render(await SiteFooter());
    expect(document.querySelector('footer a[href="/support#contact"]')).not.toBeNull();
    expect(document.querySelector('footer a[href="/support#grievance"]')).toBeNull();
    cleanup();

    vi.stubEnv("FRESH_GRIEVANCE_OFFICER_NAME", "A. Officer");
    vi.stubEnv("FRESH_GRIEVANCE_OFFICER_EMAIL", "grievance@example.in");
    vi.stubEnv("FRESH_GRIEVANCE_RESPONSE_TIME", "Within 48 hours");
    render(await SiteFooter());
    expect(document.querySelector('footer a[href="/support#grievance"]')).not.toBeNull();
  });
});
