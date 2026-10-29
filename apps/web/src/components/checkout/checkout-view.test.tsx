import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { cartWithSeer, homeAddress, preview, problem } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { routerMock } from "../../../test/router-mock";
import { CheckoutView } from "./checkout-view";

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

const placedOrder = {
  id: "o1",
  orderNumber: "FO-260001",
  status: "CONFIRMED",
};

describe("CheckoutView", () => {
  it("on 409 PRICE_CHANGED shows the new server total and confirms with a new Idempotency-Key", async () => {
    const placeCalls: { key: string | null; body: Record<string, unknown> }[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, ({ request }) => {
        expect(new URL(request.url).searchParams.get("pincode")).toBe("600041");
        return HttpResponse.json(slots);
      }),
      http.post(`${API}/checkout/preview`, () => HttpResponse.json(preview(66950))),
      http.post(`${API}/orders`, async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        placeCalls.push({ key: request.headers.get("Idempotency-Key"), body });
        if (placeCalls.length === 1) {
          return HttpResponse.json(problem(409, "PRICE_CHANGED", { preview: preview(68950) }), {
            status: 409,
          });
        }
        return HttpResponse.json(placedOrder, { status: 201 });
      }),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    const [available, full] = within(times).getAllByRole("radio");
    expect(full).toBeDisabled();
    await userEvent.click(available!);

    const placeButton = await screen.findByRole("button", { name: "Place order · ₹669.50" });
    await userEvent.click(placeButton);

    const dialog = await screen.findByRole("dialog", { name: "Your total has changed" });
    expect(within(dialog).getByText("₹669.50")).toBeInTheDocument();
    expect(within(dialog).getByText("₹689.50")).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole("button", { name: "Place order at ₹689.50" }));

    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith("/orders/o1/success"));
    expect(placeCalls).toHaveLength(2);
    expect(placeCalls[0]!.body).toMatchObject({
      addressId: "a1",
      deliverySlotId: "slot1",
      paymentMethod: "COD",
      substitutionPreference: "CALL_ME",
      expectedTotalPaise: 66950,
    });
    // The body never carries lines or prices other than the total the server last quoted.
    expect(Object.keys(placeCalls[0]!.body)).not.toContain("lines");
    expect(placeCalls[1]!.body.expectedTotalPaise).toBe(68950);
    expect(placeCalls[0]!.key).toBeTruthy();
    expect(placeCalls[1]!.key).toBeTruthy();
    expect(placeCalls[1]!.key).not.toBe(placeCalls[0]!.key);
  });

  it("prices the slot chosen in the cart and pays by the chosen on-delivery method", async () => {
    const previews: Record<string, unknown>[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.post(`${API}/checkout/preview`, async ({ request }) => {
        previews.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json(preview(66950));
      }),
    );
    renderWithProviders(<CheckoutView initialSlotId="slot1" />, { session: signedIn });

    const bar = await screen.findByRole("region", { name: "Order total" });
    expect(await within(bar).findByRole("button", { name: "Place order · ₹669.50" })).toBeEnabled();
    expect(previews[0]).toMatchObject({ deliverySlotId: "slot1", paymentMethod: "COD" });
    // ONLINE is offered by the server but not enabled for this order.
    expect(screen.getByRole("radio", { name: "Pay online" })).toBeDisabled();

    await userEvent.click(screen.getByRole("radio", { name: "UPI on delivery" }));
    await waitFor(() =>
      expect(previews.at(-1)).toMatchObject({ paymentMethod: "UPI_ON_DELIVERY" }),
    );
    expect(screen.getByRole("radio", { name: "UPI on delivery" })).toBeChecked();
  });

  it("reuses the same Idempotency-Key when retrying after a network failure", async () => {
    const keys: (string | null)[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.post(`${API}/checkout/preview`, () => HttpResponse.json(preview(66950))),
      http.post(`${API}/orders`, ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        if (keys.length === 1) return HttpResponse.error();
        return HttpResponse.json(placedOrder, { status: 201 });
      }),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    await userEvent.click(within(times).getAllByRole("radio")[0]!);
    const placeButton = await screen.findByRole("button", { name: /Place order · / });
    await userEvent.click(placeButton);
    expect(await screen.findByRole("alert")).toBeInTheDocument();

    await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith("/orders/o1/success"));
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it.each([500, 502, 504])(
    "keeps the Idempotency-Key after an unclear %i, so a retry cannot place a second order (W-4)",
    async (status) => {
      const keys: (string | null)[] = [];
      server.use(
        http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
        http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
        http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
        http.post(`${API}/checkout/preview`, () => HttpResponse.json(preview(66950))),
        http.post(`${API}/orders`, ({ request }) => {
          keys.push(request.headers.get("Idempotency-Key"));
          if (keys.length === 1) return new HttpResponse("Bad gateway", { status });
          return HttpResponse.json(placedOrder, { status: 201 });
        }),
      );
      renderWithProviders(<CheckoutView />, { session: signedIn });

      const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
      await userEvent.click(within(times).getAllByRole("radio")[0]!);
      await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
      expect(await screen.findByRole("alert")).toBeInTheDocument();

      await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
      await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith("/orders/o1/success"));
      expect(keys).toHaveLength(2);
      expect(keys[1]).toBe(keys[0]);
    },
  );

  it("takes a new Idempotency-Key after a definite refusal (W-4)", async () => {
    const keys: (string | null)[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.post(`${API}/checkout/preview`, () => HttpResponse.json(preview(66950))),
      http.post(`${API}/orders`, ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        if (keys.length === 1) {
          return HttpResponse.json(problem(422, "SLOT_FULL"), { status: 422 });
        }
        return HttpResponse.json(placedOrder, { status: 201 });
      }),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    await userEvent.click(within(times).getAllByRole("radio")[0]!);
    await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
    expect(await screen.findByRole("alert")).toBeInTheDocument();

    await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith("/orders/o1/success"));
    expect(keys).toHaveLength(2);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it("takes a new Idempotency-Key when the payment method changes before a retry", async () => {
    const keys: (string | null)[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.post(`${API}/checkout/preview`, () => HttpResponse.json(preview(66950))),
      http.post(`${API}/orders`, ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        if (keys.length === 1) return HttpResponse.error();
        return HttpResponse.json(placedOrder, { status: 201 });
      }),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    await userEvent.click(within(times).getAllByRole("radio")[0]!);
    await userEvent.click(await screen.findByRole("button", { name: /Place order · / }));
    expect(await screen.findByRole("alert")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: "UPI on delivery" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: /Place order · / })).toBeEnabled(),
    );
    await userEvent.click(screen.getByRole("button", { name: /Place order · / }));
    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith("/orders/o1/success"));
    expect(keys).toHaveLength(2);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it("edits the chosen address with If-Match and the server's version", async () => {
    let put: { ifMatch: string | null; body: Record<string, unknown> } | null = null;
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => HttpResponse.json([homeAddress])),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.put(`${API}/me/addresses/a1`, async ({ request }) => {
        put = {
          ifMatch: request.headers.get("If-Match"),
          body: (await request.json()) as Record<string, unknown>,
        };
        return HttpResponse.json({ ...homeAddress, line1: "14, 3rd Cross Street", version: "12" });
      }),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Edit this address" }));
    const dialog = await screen.findByRole("dialog", { name: "Edit delivery address" });
    const line1 = within(dialog).getByLabelText(/House, flat and street/);
    await userEvent.clear(line1);
    await userEvent.type(line1, "14, 3rd Cross Street");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Save and use this address" }),
    );

    await waitFor(() => expect(put).not.toBeNull());
    expect(put!.ifMatch).toContain("11");
    expect(put!.body).toMatchObject({ line1: "14, 3rd Cross Street", pincode: "600041" });
    await waitFor(() =>
      expect(screen.queryByRole("dialog", { name: "Edit delivery address" })).toBeNull(),
    );
  });

  it("reloads the addresses when the edit quotes a stale version (412)", async () => {
    let reads = 0;
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/me/addresses`, () => {
        reads += 1;
        return HttpResponse.json([homeAddress]);
      }),
      http.get(`${API}/delivery-slots`, () => HttpResponse.json(slots)),
      http.put(`${API}/me/addresses/a1`, () =>
        HttpResponse.json(problem(412, "CONCURRENCY_CONFLICT"), { status: 412 }),
      ),
    );
    renderWithProviders(<CheckoutView />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Edit this address" }));
    const dialog = await screen.findByRole("dialog", { name: "Edit delivery address" });
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Save and use this address" }),
    );

    await waitFor(() => expect(reads).toBeGreaterThanOrEqual(2));
    await waitFor(() =>
      expect(screen.queryByRole("dialog", { name: "Edit delivery address" })).toBeNull(),
    );
  });
});
