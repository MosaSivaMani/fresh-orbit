import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, emptyCart, problem, seerFish } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
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
});
