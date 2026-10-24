import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, emptyCart, problem, seerFish, seerLine } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { CartView, checkoutHref } from "./cart-view";

const slots = {
  fulfillmentLocationId: "l1",
  items: [
    {
      id: "slot1",
      slotDate: "2026-10-23",
      startAt: "2026-10-23T08:30:00Z",
      endAt: "2026-10-23T10:30:00Z",
      cutoffAt: "2026-10-23T06:30:00Z",
      status: "AVAILABLE",
    },
    {
      id: "slot2",
      slotDate: "2026-10-23",
      startAt: "2026-10-23T10:30:00Z",
      endAt: "2026-10-23T12:30:00Z",
      cutoffAt: "2026-10-23T08:30:00Z",
      status: "FULL",
    },
  ],
};

/** The reads a priced cart page makes: the cart, the product (for stepper bounds) and the slots. */
function cartReads(cart = cartWithSeer) {
  return [
    http.get(`${API}/cart`, () => HttpResponse.json(cart)),
    http.get(`${API}/products/:slug`, () => HttpResponse.json(seerFish)),
    http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
  ];
}

describe("CartView", () => {
  it("renders the server's line totals and bill without computing them", async () => {
    server.use(...cartReads());
    renderWithProviders(<CartView />);

    const items = await screen.findByRole("list", { name: "Items in your cart" });
    expect(within(items).getByText("₹669.50")).toBeInTheDocument();
    expect(within(items).getByText("at most ₹736.45")).toBeInTheDocument();
    const bill = screen.getByRole("region", { name: "Bill details" });
    expect(within(bill).getByText("Free")).toBeInTheDocument();
    expect(within(bill).getByText("₹669.50")).toBeInTheDocument();
    const bar = screen.getByRole("region", { name: "Order total" });
    expect(within(bar).getByText("₹669.50")).toBeInTheDocument();
    expect(within(bar).getByRole("link", { name: "Sign in to place order" })).toHaveAttribute(
      "href",
      "/login?next=%2Fcheckout",
    );
  });

  it("steps the quantity by the product's order step and shows the server's new line", async () => {
    let patched: unknown;
    server.use(
      ...cartReads(),
      http.patch(`${API}/cart/items/line1`, async ({ request }) => {
        patched = await request.json();
        return HttpResponse.json({
          ...cartWithSeer,
          lines: [
            {
              ...seerLine,
              quantity: { value: 750, unit: "GRAM" },
              estimatedLineTotalPaise: 100425,
            },
          ],
        });
      }),
    );
    renderWithProviders(<CartView />);

    const increase = await screen.findByRole("button", {
      name: "Increase quantity of Seer Fish (Vanjaram)",
    });
    await waitFor(() => expect(increase).toBeEnabled());
    await userEvent.click(increase);

    // 500 g + the variant's 250 g step; the new total is whatever the server returned.
    await waitFor(() => expect(patched).toEqual({ quantity: { value: 750, unit: "GRAM" } }));
    const items = screen.getByRole("list", { name: "Items in your cart" });
    expect(await within(items).findByText("₹1,004.25")).toBeInTheDocument();
  });

  it("removes the line when stepping below the minimum", async () => {
    let deleted = false;
    server.use(
      ...cartReads(),
      http.delete(`${API}/cart/items/line1`, () => {
        deleted = true;
        return HttpResponse.json(emptyCart);
      }),
    );
    renderWithProviders(<CartView />);

    const remove = await screen.findByRole("button", {
      name: "Remove Seer Fish (Vanjaram) from cart",
    });
    await waitFor(() => expect(remove).toBeEnabled());
    await userEvent.click(remove);

    await waitFor(() => expect(deleted).toBe(true));
    expect(await screen.findByText("Your cart is empty")).toBeInTheDocument();
  });

  it("carries the chosen slot button to checkout", async () => {
    server.use(...cartReads());
    renderWithProviders(<CartView />, { session: signedIn });

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    const [available, full] = within(times).getAllByRole("radio");
    expect(full).toBeDisabled();
    await userEvent.click(available!);

    expect(screen.getByRole("link", { name: "Place order" })).toHaveAttribute(
      "href",
      "/checkout?slot=slot1",
    );
  });

  it("shows the server's coupon refusal next to the field", async () => {
    server.use(
      ...cartReads(),
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

  it("shows the empty-cart character state for an empty cart", async () => {
    server.use(http.get(`${API}/cart`, () => HttpResponse.json(emptyCart)));
    const { container } = renderWithProviders(<CartView />);
    expect(await screen.findByRole("heading", { name: "Your cart is empty" })).toBeInTheDocument();
    expect(container.querySelector("img")?.getAttribute("src")).toContain("empty");
    expect(screen.getByRole("link", { name: "Start shopping" })).toHaveAttribute("href", "/shop");
  });

  it("shows the character error state with the trace id when the cart cannot be read", async () => {
    server.use(
      http.get(`${API}/cart`, () =>
        HttpResponse.json(problem(500, "INTERNAL_ERROR"), { status: 500 }),
      ),
    );
    renderWithProviders(<CartView />);
    const alert = await screen.findByRole("alert");
    expect(within(alert).getByText("We could not load your cart")).toBeInTheDocument();
    expect(within(alert).getByText("Reference: 00-test-trace-01")).toBeInTheDocument();
    expect(within(alert).getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});

describe("checkoutHref", () => {
  it("goes through sign-in for a guest and keeps the slot", () => {
    expect(checkoutHref(true, null)).toBe("/checkout");
    expect(checkoutHref(true, "s 1")).toBe("/checkout?slot=s%201");
    expect(checkoutHref(false, "slot1")).toBe("/login?next=%2Fcheckout%3Fslot%3Dslot1");
  });
});
