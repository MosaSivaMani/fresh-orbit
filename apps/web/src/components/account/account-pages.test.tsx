import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { sessionStore } from "@/auth/session-store";
import { homeAddress, problem } from "../../../test/fixtures";
import { API, BFF } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { navigationState, routerMock } from "../../../test/router-mock";
import { AccountNav } from "./account-nav";
import { AddressesView } from "./addresses-view";
import { OrdersList } from "./orders-list";
import { ProfileView } from "./profile-view";
import { SettingsView } from "./settings-view";

const profile = {
  id: "u1",
  phone: "+919876543210",
  fullName: "Priya Raman",
  email: null,
  marketingOptIn: false,
  status: "ACTIVE",
  version: "7",
};

const preferences = {
  preferences: [
    { category: "ORDER_UPDATES", channel: "SMS", enabled: true },
    { category: "OFFERS", channel: "SMS", enabled: false },
  ],
};

describe("AccountNav", () => {
  it("marks the current section, settings included", () => {
    navigationState.pathname = "/account/settings";
    renderWithProviders(<AccountNav />);
    const nav = screen.getByRole("navigation", { name: "Account" });
    expect(within(nav).getByRole("link", { name: "Settings" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(nav).getByRole("link", { name: "Profile" })).not.toHaveAttribute("aria-current");
  });
});

describe("SettingsView", () => {
  it("shows the locked order-updates SMS switch and signs out through the BFF", async () => {
    let loggedOut = false;
    server.use(
      http.get(`${API}/me`, () => HttpResponse.json(profile)),
      http.get(`${API}/me/notification-preferences`, () => HttpResponse.json(preferences)),
      http.post(`${BFF}/logout`, () => {
        loggedOut = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<SettingsView />, { session: signedIn });

    const [orderSms, offersSms] = await screen.findAllByRole("switch", { name: /SMS/ });
    expect(orderSms).toBeDisabled();
    expect(offersSms).toBeEnabled();
    expect(await screen.findByRole("button", { name: "Delete my account" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith("/"));
    expect(loggedOut).toBe(true);
    expect(sessionStore.getState().status).toBe("signedOut");
  });
});

describe("AddressesView", () => {
  it("shows the empty-addresses character when nothing is saved", async () => {
    server.use(http.get(`${API}/me/addresses`, () => HttpResponse.json([])));
    const { container } = renderWithProviders(<AddressesView />, { session: signedIn });
    expect(await screen.findByRole("heading", { name: "No saved addresses" })).toBeInTheDocument();
    expect(container.querySelector("img")?.getAttribute("src")).toContain("empty_addresses");
  });

  it("lists saved addresses with their labels", async () => {
    server.use(http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])));
    renderWithProviders(<AddressesView />, { session: signedIn });
    const list = await screen.findByRole("list", { name: "Saved addresses" });
    expect(within(list).getByText("Priya Raman")).toBeInTheDocument();
    expect(within(list).getByText("Default")).toBeInTheDocument();
  });
});

describe("OrdersList", () => {
  it("links each order to its tracking page with the server's total", async () => {
    server.use(
      http.get(`${API}/orders`, () =>
        HttpResponse.json({
          items: [
            {
              id: "o1",
              orderNumber: "FO-260001",
              status: "OUT_FOR_DELIVERY",
              paymentMethod: "COD",
              placedAt: "2026-10-23T05:41:12Z",
              slotStartAt: "2026-10-23T08:30:00Z",
              itemCount: 2,
              totalPaise: 66950,
              finalTotalPaise: 64950,
            },
          ],
          page: 1,
          pageSize: 10,
          totalCount: 1,
        }),
      ),
    );
    renderWithProviders(<OrdersList />, { session: signedIn });
    const list = await screen.findByRole("list", { name: "Your orders" });
    const link = within(list).getByRole("link");
    expect(link).toHaveAttribute("href", "/orders/o1");
    expect(within(link).getByText("₹649.50")).toBeInTheDocument();
    expect(within(link).getByText("Out for delivery")).toBeInTheDocument();
  });

  it("shows the empty-orders character when there are none", async () => {
    server.use(
      http.get(`${API}/orders`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 10, totalCount: 0 }),
      ),
    );
    renderWithProviders(<OrdersList />, { session: signedIn });
    expect(await screen.findByRole("heading", { name: "No orders yet" })).toBeInTheDocument();
  });
});

describe("ProfileView", () => {
  it("saves with If-Match and reloads the profile after a lost race (409 CONCURRENT_SAVE)", async () => {
    let reads = 0;
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/me`, () => {
        reads += 1;
        return HttpResponse.json(reads === 1 ? profile : { ...profile, version: "8" });
      }),
      http.patch(`${API}/me`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json(problem(409, "CONCURRENT_SAVE"), { status: 409 });
      }),
    );
    const { container } = renderWithProviders(<ProfileView />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(reads).toBeGreaterThanOrEqual(2));
    expect(ifMatch).toContain("7");
    // The notice survives the form starting again with the new version.
    await waitFor(() =>
      expect(container.querySelector('[data-code="CONCURRENCY_CONFLICT"]')).not.toBeNull(),
    );
  });
});

describe("DeleteAccount", () => {
  it("re-reads the profile when the delete quotes a stale version (412)", async () => {
    let reads = 0;
    let deletes = 0;
    server.use(
      http.get(`${API}/me`, () => {
        reads += 1;
        return HttpResponse.json(profile);
      }),
      http.get(`${API}/me/notification-preferences`, () => HttpResponse.json(preferences)),
      http.delete(`${API}/me`, () => {
        deletes += 1;
        return HttpResponse.json(problem(412, "CONCURRENCY_CONFLICT"), { status: 412 });
      }),
    );
    renderWithProviders(<SettingsView />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Delete my account" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByRole("textbox"), "DELETE");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete account" }));

    await waitFor(() => expect(deletes).toBe(1));
    await waitFor(() => expect(reads).toBeGreaterThanOrEqual(2));
    expect(within(dialog).getByRole("alert")).toBeInTheDocument();
    expect(routerMock.replace).not.toHaveBeenCalled();
  });
});
