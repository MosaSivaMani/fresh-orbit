import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { fakeAccessToken, renderApp } from "../test/render";
import { emptyDashboard } from "../test/fixtures";
import { API, problem, server } from "../test/server";

describe("admin shell", () => {
  it("sends signed-out visitors to the login page and keeps where they were going", async () => {
    const { router } = renderApp({ permissions: null, path: "/orders?status=CONFIRMED" });
    expect(
      await screen.findByRole("heading", { name: "Sign in to Fresh Orbitt Admin" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/login");
    expect(router.state.location.search).toContain("next=%2Forders%3Fstatus%3DCONFIRMED");
  });

  it("shows only the menu entries the staff member's permissions allow", async () => {
    renderApp({ permissions: ["orders.view", "orders.pack", "inventory.view"] });
    const nav = (await screen.findAllByRole("navigation", { name: "Main" }))[0]!;
    expect(within(nav).getByRole("link", { name: "Orders" })).toBeInTheDocument();
    expect(within(nav).getByRole("link", { name: "Inventory" })).toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Pricing" })).not.toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Staff & roles" })).not.toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Reports" })).not.toBeInTheDocument();
  });

  it("shows the no-access page for a route the staff member cannot use", async () => {
    renderApp({ permissions: ["orders.view"], path: "/pricing" });
    expect(await screen.findByText("You do not have access to this page")).toBeInTheDocument();
  });

  it("ends the session and returns to login when the API refuses the token", async () => {
    server.use(http.get(`${API}/admin/reports/dashboard`, () => problem(401, "UNAUTHENTICATED")));
    const { router } = renderApp({ permissions: ["reports.view"] });
    expect(
      await screen.findByText("Your session ended. Sign in again to continue."),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/login");
  });

  it("refreshes an expired access token once and retries the request", async () => {
    let calls = 0;
    const newAccess = fakeAccessToken({ permissions: ["reports.view"], locationIds: ["rotated"] });
    server.use(
      http.get(`${API}/admin/reports/dashboard`, ({ request }) => {
        calls += 1;
        if (request.headers.get("Authorization") === `Bearer ${newAccess}`) {
          return HttpResponse.json(emptyDashboard());
        }
        return problem(401, "TOKEN_EXPIRED");
      }),
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json({ accessToken: newAccess, refreshToken: "rt_new" }),
      ),
    );
    renderApp({ permissions: ["reports.view"] });
    expect(await screen.findByText("No delivery slots today.")).toBeInTheDocument();
    expect(calls).toBe(2);
  });

  it("offers a location switcher when the staff member works at several locations", async () => {
    renderApp({ permissions: ["orders.view"], locationIds: ["loc-a-000001", "loc-b-000002"] });
    expect(
      await screen.findByRole("combobox", { name: "Fulfillment location" }),
    ).toBeInTheDocument();
  });
});
