import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type OrderDetail, type OrderTracking } from "@/api/types/orders";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { OrderDetailView } from "./order-detail-view";

const amounts = {
  itemsPaise: 64950,
  preparationPaise: 2000,
  discountPaise: 0,
  deliveryFeePaise: 0,
  taxPaise: 0,
  totalPaise: 66950,
};

function order(overrides: Partial<OrderDetail> = {}): OrderDetail {
  return {
    id: "o1",
    orderNumber: "FO-260001",
    version: "1",
    status: "CONFIRMED",
    paymentMethod: "COD",
    placedAt: "2026-10-23T05:41:12Z",
    slot: { startAt: "2026-10-23T08:30:00Z", endAt: "2026-10-23T10:30:00Z" },
    address: {
      recipientName: "Priya Raman",
      phone: "+919876543210",
      line1: "12, 3rd Cross Street",
      line2: null,
      landmark: null,
      locality: "Thiruvanmiyur",
      cityName: "Chennai",
      stateCode: "TN",
      pincode: "600041",
    },
    substitutionPreference: "CALL_ME",
    items: [],
    estimate: amounts,
    final: null,
    amountDuePaise: 66950,
    amountPaidPaise: 0,
    amountRefundedPaise: 0,
    couponCode: null,
    customerNotes: null,
    substitutions: [],
    payment: { method: "COD", status: "PENDING", nextAction: null },
    invoice: null,
    allowedActions: { cancel: true, raiseComplaint: false, downloadInvoice: false, reorder: false },
    ...overrides,
  };
}

function tracking(overrides: Partial<OrderTracking> = {}): OrderTracking {
  return {
    orderNumber: "FO-260001",
    status: "CONFIRMED",
    timeline: [{ status: "CONFIRMED", at: "2026-10-23T05:41:12Z" }],
    slot: { startAt: "2026-10-23T08:30:00Z", endAt: "2026-10-23T10:30:00Z" },
    shipment: null,
    deliveryCode: null,
    amountDuePaise: 66950,
    ...overrides,
  };
}

describe("OrderDetailView", () => {
  it("shows the delivery code only when the server sends it", async () => {
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json(
          order({
            status: "OUT_FOR_DELIVERY",
            allowedActions: {
              cancel: false,
              raiseComplaint: false,
              downloadInvoice: false,
              reorder: false,
            },
          }),
        ),
      ),
      http.get(`${API}/orders/o1/tracking`, () =>
        HttpResponse.json(tracking({ status: "OUT_FOR_DELIVERY", deliveryCode: "4821" })),
      ),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    expect(await screen.findByText("4821")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel order" })).not.toBeInTheDocument();
    expect(screen.getByText("To pay on delivery: ₹669.50")).toBeInTheDocument();
  });

  it("shows tracking steps up to the server's status and the server's bill", async () => {
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json(
          order({
            status: "PACKED",
            final: { ...amounts, discountPaise: 5000, totalPaise: 61950 },
          }),
        ),
      ),
      http.get(`${API}/orders/o1/tracking`, () =>
        HttpResponse.json(
          tracking({
            status: "PACKED",
            timeline: [
              { status: "CONFIRMED", at: "2026-10-23T05:41:12Z" },
              { status: "PACKED", at: "2026-10-23T07:00:00Z" },
            ],
          }),
        ),
      ),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    const steps = await screen.findByRole("list", { name: "Order progress" });
    const items = within(steps).getAllByRole("listitem");
    expect(items).toHaveLength(5);
    expect(items[2]).toHaveAttribute("aria-current", "step");
    expect(items[2]).toHaveTextContent("Packed");
    expect(items[3]).toHaveAttribute("data-state", "upcoming");

    const bill = screen.getByRole("region", { name: "Bill" });
    expect(within(bill).getByText("− ₹50")).toBeInTheDocument();
    // The final total is the server's value, shown as "Total".
    expect(within(bill).getByText("Total")).toBeInTheDocument();
    expect(within(bill).getByText("₹619.50")).toBeInTheDocument();
  });

  it("shows the not-found character state for an unknown order", async () => {
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json(
          { status: 404, code: "ORDER_NOT_FOUND", traceId: "t-404" },
          { status: 404 },
        ),
      ),
      http.get(`${API}/orders/o1/tracking`, () =>
        HttpResponse.json({ status: 404, code: "ORDER_NOT_FOUND" }, { status: 404 }),
      ),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    const alert = await screen.findByRole("alert");
    expect(within(alert).getByRole("heading", { name: "Order not found" })).toBeInTheDocument();
    expect(within(alert).queryByRole("button", { name: "Try again" })).not.toBeInTheDocument();
  });

  it("cancels with the chosen reason and an Idempotency-Key when the server allows it", async () => {
    let cancel: { key: string | null; body: unknown } | null = null;
    server.use(
      http.get(`${API}/orders/o1`, () => HttpResponse.json(order())),
      http.get(`${API}/orders/o1/tracking`, () => HttpResponse.json(tracking())),
      http.post(`${API}/orders/o1/cancel`, async ({ request }) => {
        cancel = { key: request.headers.get("Idempotency-Key"), body: await request.json() };
        return HttpResponse.json(
          order({
            status: "CANCELLED",
            allowedActions: {
              cancel: false,
              raiseComplaint: false,
              downloadInvoice: false,
              reorder: true,
            },
          }),
        );
      }),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Cancel order" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByLabelText("Wrong address"));
    await userEvent.click(within(dialog).getByRole("button", { name: "Cancel order" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Order again" })).toBeInTheDocument(),
    );
    expect(cancel).toMatchObject({ body: { reasonCode: "WRONG_ADDRESS" } });
    expect(cancel!.key).toBeTruthy();
    expect(screen.queryByText("4821")).not.toBeInTheDocument();
  });

  it("lists a proposed substitution and accepts it through the server", async () => {
    const proposed = {
      id: "s1",
      orderItemId: "i1",
      proposedName: "Seer fish steaks",
      proposedQuantity: { value: 500, unit: "GRAM" },
      priceDifferencePaise: 4000,
      status: "PROPOSED",
      expiresAt: "2026-10-23T07:30:00Z",
      respondedAt: null,
      newOrderItemId: null,
    };
    let accepted = false;
    server.use(
      http.get(`${API}/orders/o1`, () => HttpResponse.json(order({ substitutions: [proposed] }))),
      http.get(`${API}/orders/o1/tracking`, () => HttpResponse.json(tracking())),
      http.post(`${API}/orders/o1/substitutions/s1/accept`, () => {
        accepted = true;
        return HttpResponse.json(
          order({
            substitutions: [
              { ...proposed, status: "ACCEPTED", respondedAt: "2026-10-23T06:00:00Z" },
            ],
          }),
        );
      }),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    const card = await screen.findByRole("region", { name: "Substitutions" });
    expect(within(card).getByRole("button", { name: "Reject Seer fish steaks" })).toBeEnabled();
    await userEvent.click(within(card).getByRole("button", { name: "Accept Seer fish steaks" }));

    await waitFor(() => expect(accepted).toBe(true));
    await waitFor(() =>
      expect(
        within(card).queryByRole("button", { name: "Accept Seer fish steaks" }),
      ).not.toBeInTheDocument(),
    );
  });

  it("sends the complaint, then each chosen photo to the new complaint", async () => {
    const photoPosts: string[] = [];
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json(
          order({
            status: "DELIVERED",
            allowedActions: {
              cancel: false,
              raiseComplaint: true,
              downloadInvoice: false,
              reorder: false,
            },
          }),
        ),
      ),
      http.get(`${API}/orders/o1/tracking`, () =>
        HttpResponse.json(tracking({ status: "DELIVERED" })),
      ),
      http.get(`${API}/orders/o1/complaints`, () => HttpResponse.json([])),
      http.post(`${API}/orders/o1/complaints`, () =>
        HttpResponse.json({ id: "c1", status: "OPEN", photos: [] }, { status: 201 }),
      ),
      http.post(`${API}/orders/o1/complaints/c1/photos`, async ({ request }) => {
        const form = await request.formData();
        const file = form.get("file");
        // The test DOM and the fetch layer have separate File classes, so the file's name does not survive the trip;
        // check that a file part arrived.
        photoPosts.push(file === null || typeof file === "string" ? "missing" : "file");
        return HttpResponse.json({ id: "c1", status: "OPEN", photos: [] }, { status: 201 });
      }),
    );
    renderWithProviders(<OrderDetailView orderId="o1" />, { session: signedIn });

    await userEvent.click(await screen.findByRole("button", { name: "Report a problem" }));
    const sheet = await screen.findByRole("dialog");
    await userEvent.type(
      within(sheet).getByLabelText(/Details/),
      "The fish smelled off on arrival.",
    );
    const photo = new File(["x"], "fish.jpg", { type: "image/jpeg" });
    await userEvent.upload(within(sheet).getByLabelText(/Photos/), photo);
    await userEvent.click(within(sheet).getByRole("button", { name: "Send" }));

    await waitFor(() => expect(photoPosts).toEqual(["file"]));
  });
});
