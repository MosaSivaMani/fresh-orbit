import { render } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { type PublicBusinessProfile } from "@/api/types/locations";

const { getBusinessProfile, publicHttp } = vi.hoisted(() => ({
  getBusinessProfile: vi.fn<() => Promise<PublicBusinessProfile>>(),
  publicHttp: vi.fn((revalidateSeconds: number) => ({ revalidateSeconds })),
}));

vi.mock("@/api/endpoints/catalog", () => ({ getBusinessProfile }));
vi.mock("@/api/server", () => ({ publicHttp }));

const { default: DeleteAccountPage } = await import("./page");

const profile: PublicBusinessProfile = {
  legalName: "Example Seafoods Private Limited",
  tradeName: "Example",
  gstin: null,
  fssaiLicenseNo: null,
  address: "1 Harbour Road",
  supportPhone: "+919876543210",
  supportEmail: "help@example.in",
  supportHours: null,
  timeZone: "Asia/Kolkata",
};

describe("delete account page (L-82, D376)", () => {
  beforeEach(() => {
    getBusinessProfile.mockReset();
  });

  it("explains every way to delete, with the support e-mail from the business profile", async () => {
    getBusinessProfile.mockResolvedValue(profile);

    render(await DeleteAccountPage());

    for (const id of ["in-the-app", "by-email", "deleted", "kept", "before"]) {
      expect(document.getElementById(id)).not.toBeNull();
    }
    expect(document.querySelector('a[href="/account/settings"]')).not.toBeNull();
    expect(document.querySelector('#by-email a[href="mailto:help@example.in"]')).not.toBeNull();
    expect(document.querySelector('a[href="/support/privacy-policy"]')).not.toBeNull();
  });

  it("points to the Help page when the business profile is unavailable", async () => {
    getBusinessProfile.mockRejectedValue(new Error("fetch failed"));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    render(await DeleteAccountPage());

    expect(document.querySelector('#by-email a[href^="mailto:"]')).toBeNull();
    expect(document.querySelector('#by-email a[href="/support#contact"]')).not.toBeNull();
    warn.mockRestore();
  });
});
