import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, emptyCart, problem, seerFish } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { PINCODE_COOKIE, writeBrowserCookie } from "@/lib/cookies";
import { PurchasePanel } from "./purchase-panel";

describe("PurchasePanel", () => {
  it("adds the chosen variant, preparation and weight, then shows the server's estimate and maximum", async () => {
    writeBrowserCookie(PINCODE_COOKIE, "600041");
    let cart = emptyCart;
    let sent: { body: unknown; pincode: string | null } | null = null;
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cart)),
      http.post(`${API}/cart/items`, async ({ request }) => {
        sent = { body: await request.json(), pincode: request.headers.get("X-Pincode") };
        cart = cartWithSeer;
        return HttpResponse.json(cartWithSeer);
      }),
    );
    renderWithProviders(<PurchasePanel product={seerFish} />);

    const add = await screen.findByRole("button", { name: "Add to cart" });
    await userEvent.click(add);

    expect(await screen.findByText(/In your cart: 500 g/)).toBeInTheDocument();
    expect(sent).toEqual({
      body: { variantId: "v1", preparationCode: "STEAK", quantity: { value: 500, unit: "GRAM" } },
      pincode: "600041",
    });
    // Both amounts are the server's cart line values (₹669.50 and ₹736.45), only formatted here.
    expect(screen.getByText(/Estimated ₹669\.50/)).toBeInTheDocument();
    expect(screen.getByText(/at most ₹736\.45/)).toBeInTheDocument();
  });

  it("shows the server's field message when the quantity is refused", async () => {
    writeBrowserCookie(PINCODE_COOKIE, "600041");
    server.use(
      http.post(`${API}/cart/items`, () =>
        HttpResponse.json(
          problem(422, "QUANTITY_ABOVE_AVAILABLE", {
            errors: {
              "quantity.value": [
                { code: "QUANTITY_ABOVE_AVAILABLE", message: "Choose at most 2 kg." },
              ],
            },
          }),
          { status: 422 },
        ),
      ),
    );
    renderWithProviders(<PurchasePanel product={seerFish} />);

    await userEvent.click(await screen.findByRole("button", { name: "Add to cart" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Choose at most 2 kg.");
  });

  it("asks for a pincode instead of adding when none is set", async () => {
    renderWithProviders(
      <PurchasePanel
        product={{
          ...seerFish,
          variants: seerFish.variants.map((v) => ({
            ...v,
            unitPricePaise: null,
            compareAtPaise: null,
            availability: null,
          })),
        }}
      />,
    );
    expect(await screen.findByText("Set your pincode to see the price.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Set pincode to buy" })).toBeEnabled();
  });

  it("asks the server to notify a signed-in customer when a sold-out size is back", async () => {
    writeBrowserCookie(PINCODE_COOKIE, "600041");
    let created: unknown = null;
    server.use(
      http.get(`${API}/me/stock-alerts`, () => HttpResponse.json([])),
      http.get(`${API}/serviceability`, () =>
        HttpResponse.json({
          pincode: "600041",
          serviceable: true,
          fulfillmentLocation: { id: "loc1", code: "L1", name: "Store", hasColdStorage: true },
        }),
      ),
      http.post(`${API}/me/stock-alerts`, async ({ request }) => {
        created = await request.json();
        return HttpResponse.json(
          { id: "a1", variantId: "v1", fulfillmentLocationId: "loc1", createdAt: "2026-10-23" },
          { status: 201 },
        );
      }),
    );
    const soldOut = {
      ...seerFish,
      variants: seerFish.variants.map((v) => ({
        ...v,
        availability: { status: "OUT_OF_STOCK", maxOrderableNow: null, message: null },
      })),
    } as typeof seerFish;
    renderWithProviders(<PurchasePanel product={soldOut} />, { session: signedIn });

    const notify = await screen.findByRole("button", { name: "Notify me when it is back" });
    await waitFor(() => expect(notify).toBeEnabled());
    await userEvent.click(notify);

    await waitFor(() =>
      expect(created).toEqual({ variantId: "v1", fulfillmentLocationId: "loc1" }),
    );
  });
});
