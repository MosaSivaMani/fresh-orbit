import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type TaxCategoryRules } from "../../api/types/pricing";
import { renderApp } from "../../test/render";
import { API, server } from "../../test/server";

const RULE: TaxCategoryRules = {
  taxCategoryCode: "GST_FRESH_FISH",
  name: "Fresh fish",
  version: "1",
  current: {
    id: "period-1",
    rateBp: 500,
    validFrom: "2026-04-01T00:00:00Z",
    validTo: null,
  },
  next: null,
  history: [],
  unchanged: false,
};

function mockRules() {
  server.use(http.get(`${API}/admin/pricing/tax-rules`, () => HttpResponse.json([RULE])));
}

describe("tax rules permissions (D35)", () => {
  it("lets tax.manage-only staff (finance) open the screen from the menu and schedule", async () => {
    mockRules();
    const { user, router } = renderApp({ permissions: ["tax.manage"] });
    const nav = (await screen.findAllByRole("navigation", { name: "Main" }))[0]!;
    expect(within(nav).queryByRole("link", { name: "Pricing" })).not.toBeInTheDocument();
    await user.click(within(nav).getByRole("link", { name: "Tax rules" }));

    expect(await screen.findByRole("heading", { name: "Tax rules" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/pricing/tax-rules");
    expect(await screen.findByText("GST_FRESH_FISH")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New tax period" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New rate for GST_FRESH_FISH" })).toBeInTheDocument();
  });

  it("shows the list to pricing.manage staff without the schedule actions", async () => {
    mockRules();
    renderApp({ permissions: ["pricing.manage"], path: "/pricing/tax-rules" });
    expect(await screen.findByText("GST_FRESH_FISH")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New tax period" })).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "New rate for GST_FRESH_FISH" }),
    ).not.toBeInTheDocument();
  });

  it("refuses staff with neither permission", async () => {
    renderApp({ permissions: ["orders.view"], path: "/pricing/tax-rules" });
    expect(await screen.findByText("You do not have access to this page")).toBeInTheDocument();
  });
});
