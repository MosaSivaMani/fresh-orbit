import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, emptyCart, seerFish } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
import { routerMock } from "../../../test/router-mock";
import { type ProductListItem } from "@/api/types/catalog";
import { PINCODE_COOKIE, writeBrowserCookie } from "@/lib/cookies";
import { ShopProductCard } from "./shop-product-card";

const seerItem: ProductListItem = {
  id: "p1",
  slug: "seer-fish-vanjaram",
  name: "Seer Fish",
  localNames: ["Vanjaram"],
  sourceType: "WILD",
  imageUrl: null,
  fromPrice: { unitPricePaise: 129900, compareAtPaise: 149900, perUnit: "KG" },
  availability: { status: "IN_STOCK", message: null },
  locationRequired: false,
};

describe("ShopProductCard", () => {
  it("adds the only size with its default cut and smallest quantity, then shows the server's stepper", async () => {
    writeBrowserCookie(PINCODE_COOKIE, "530017");
    let cart = emptyCart;
    let sent: unknown = null;
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cart)),
      http.get(`${API}/products/seer-fish-vanjaram`, () => HttpResponse.json(seerFish)),
      http.post(`${API}/cart/items`, async ({ request }) => {
        sent = await request.json();
        cart = cartWithSeer;
        return HttpResponse.json(cartWithSeer);
      }),
    );
    renderWithProviders(
      <ShopProductCard product={seerItem} offer="₹90 OFF" freshness="Next slot Today, 4 pm" />,
    );

    expect(screen.getByText("₹90 OFF")).toBeInTheDocument();
    expect(screen.getByText("Next slot Today, 4 pm")).toBeInTheDocument();
    await userEvent.click(await screen.findByRole("button", { name: "Add Seer Fish to cart" }));

    expect(
      await screen.findByRole("button", { name: "Increase quantity of Seer Fish" }),
    ).toBeInTheDocument();
    expect(sent).toEqual({
      variantId: "v1",
      preparationCode: "STEAK",
      quantity: { value: 500, unit: "GRAM" },
    });
  });

  it("opens the product page when there is more than one size to choose", async () => {
    writeBrowserCookie(PINCODE_COOKIE, "530017");
    server.use(
      http.get(`${API}/products/seer-fish-vanjaram`, () =>
        HttpResponse.json({
          ...seerFish,
          variants: [seerFish.variants[0], { ...seerFish.variants[0], id: "v2", name: "Large" }],
        }),
      ),
    );
    renderWithProviders(<ShopProductCard product={seerItem} />);

    await userEvent.click(await screen.findByRole("button", { name: "Add Seer Fish to cart" }));

    await expect
      .poll(() => routerMock.push.mock.calls.at(-1)?.[0])
      .toBe("/products/seer-fish-vanjaram");
  });

  it("has no offer line unless the server gave one", () => {
    const { container } = renderWithProviders(<ShopProductCard product={seerItem} />);
    expect(container.querySelector(".bg-gradient-to-b")).toBeNull();
  });
});
