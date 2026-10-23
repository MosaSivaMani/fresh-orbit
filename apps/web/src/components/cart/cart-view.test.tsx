import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, emptyCart, problem } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { CartView } from "./cart-view";

describe("CartView", () => {
  it("renders the server's line totals and summary without computing them", async () => {
    server.use(http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)));
    renderWithProviders(<CartView />);

    const items = await screen.findByRole("list", { name: "Items in your cart" });
    expect(within(items).getByText("₹669.50")).toBeInTheDocument();
    expect(within(items).getByText("at most ₹736.45")).toBeInTheDocument();
    expect(screen.getByText("Free")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sign in to check out" })).toHaveAttribute(
      "href",
      "/login?next=%2Fcheckout",
    );
  });

  it("shows the server's coupon refusal next to the field", async () => {
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.put(`${API}/cart/coupon`, () =>
        HttpResponse.json(
          problem(422, "COUPON_EXPIRED", {
            errors: { code: [{ code: "COUPON_EXPIRED", message: "This coupon has expired." }] },
          }),
          { status: 422 },
        ),
      ),
    );
    renderWithProviders(<CartView />, { session: signedIn });

    await userEvent.type(await screen.findByLabelText("Coupon code"), "fish50");
    await userEvent.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("This coupon has expired.")).toBeInTheDocument();
    expect(screen.getByLabelText("Coupon code")).toHaveAttribute("aria-invalid", "true");
  });

  it("shows an empty state for an empty cart", async () => {
    server.use(http.get(`${API}/cart`, () => HttpResponse.json(emptyCart)));
    renderWithProviders(<CartView />);
    expect(await screen.findByText("Your cart is empty")).toBeInTheDocument();
  });
});
