import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import {
  type CashSettlement,
  type OrderPayments,
  type PaymentSummary,
  type Refund,
} from "../../api/types/payments";
import { page } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";
import { OrderPaymentsPanel } from "./OrderPaymentsPanel";

const ORDER_ID = "0192d000-0000-7000-8000-000000000010";
const PAYMENT_ID = "0192d100-0000-7000-8000-000000000020";
const REFUND_ID = "0192d200-0000-7000-8000-000000000030";
const RIDER_ID = "0192d300-0000-7000-8000-000000000040";

function summary(overrides: Partial<PaymentSummary> = {}): PaymentSummary {
  return {
    paidPaise: 60000,
    refundedPaise: 0,
    pendingRefundPaise: 12000,
    refundablePaise: 48000,
    ...overrides,
  };
}

function refund(overrides: Partial<Refund> = {}): Refund {
  return {
    id: REFUND_ID,
    orderId: ORDER_ID,
    paymentId: PAYMENT_ID,
    fulfillmentLocationId: LOCATION_A,
    amountPaise: 12000,
    reasonCode: "COMPLAINT",
    method: "ORIGINAL",
    status: "REQUESTED",
    requestedBy: "0192c000-0000-7000-8000-000000000001",
    requestedAt: "2026-10-24T05:00:00Z",
    approvedBy: null,
    approvedAt: null,
    complaintId: null,
    payeeUpiId: null,
    notes: null,
    failureReason: null,
    providerRefundId: null,
    manualReference: null,
    succeededAt: null,
    attemptCount: 0,
    version: "41",
    paymentSummary: summary(),
    ...overrides,
  };
}

function settlement(overrides: Partial<CashSettlement> = {}): CashSettlement {
  return {
    id: "0192d400-0000-7000-8000-000000000050",
    riderId: RIDER_ID,
    fulfillmentLocationId: LOCATION_A,
    date: "2026-10-23",
    expectedPaise: 250000,
    receivedPaise: 245000,
    differencePaise: -5000,
    status: "OPEN",
    collectionsCount: 7,
    recordedBy: "0192c000-0000-7000-8000-000000000001",
    settledBy: null,
    settledAt: null,
    note: null,
    disputeReason: null,
    version: "9",
    ...overrides,
  };
}

describe("refund detail", () => {
  it("approves with If-Match and explains REFUND_SELF_APPROVAL", async () => {
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/refunds/${REFUND_ID}`, () =>
        HttpResponse.json(refund(), { headers: { ETag: '"41"' } }),
      ),
      http.post(`${API}/admin/refunds/${REFUND_ID}/approve`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return problem(422, "REFUND_SELF_APPROVAL");
      }),
    );
    const { user } = renderApp({
      path: `/refunds/${REFUND_ID}`,
      permissions: ["payments.view", "refunds.approve"],
    });

    await user.click(await screen.findByRole("button", { name: "Approve refund" }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/another staff member must approve it/i);
    expect(ifMatch).toBe('"41"');
  });

  it("hides approval actions without refunds.approve", async () => {
    server.use(http.get(`${API}/admin/refunds/${REFUND_ID}`, () => HttpResponse.json(refund())));
    renderApp({ path: `/refunds/${REFUND_ID}`, permissions: ["payments.view"] });

    expect(await screen.findByText("Order payment summary")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Approve refund" })).not.toBeInTheDocument();
  });
});

describe("cash settlements", () => {
  it("records with one Idempotency-Key across a retry and shows the server's difference", async () => {
    const keys: (string | null)[] = [];
    let body: unknown = null;
    server.use(
      http.get(`${API}/admin/cash-settlements`, () => HttpResponse.json(page([]))),
      // Staff with cash.settle only: the rider list is refused, so the form asks for the rider id.
      http.get(`${API}/admin/delivery/riders`, () => problem(403, "FORBIDDEN")),
      http.post(`${API}/admin/cash-settlements`, async ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        body = await request.json();
        if (keys.length === 1) return problem(500, "INTERNAL_ERROR");
        return HttpResponse.json(settlement(), { status: 201 });
      }),
    );
    const { user } = renderApp({
      path: "/cash-settlements",
      permissions: ["cash.settle"],
    });

    await user.click(await screen.findByRole("button", { name: "Record settlement" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Rider/), RIDER_ID);
    const date = within(dialog).getByLabelText(/^Date/);
    await user.clear(date);
    await user.type(date, "2026-10-23");
    await user.type(within(dialog).getByLabelText(/Received/), "2450");

    await user.click(within(dialog).getByRole("button", { name: "Record settlement" }));
    await waitFor(() => expect(keys).toHaveLength(1));
    // The first attempt failed; the dialog stays open with what was typed.
    await user.click(within(dialog).getByRole("button", { name: "Record settlement" }));

    expect(await screen.findByTestId("settlement-difference")).toHaveTextContent("50");
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
    expect(body).toMatchObject({ riderId: RIDER_ID, date: "2026-10-23", receivedPaise: 245000 });
  });

  it("shows SETTLEMENT_EXISTS at the date field", async () => {
    server.use(
      http.get(`${API}/admin/cash-settlements`, () => HttpResponse.json(page([]))),
      http.get(`${API}/admin/delivery/riders`, () => problem(403, "FORBIDDEN")),
      http.post(`${API}/admin/cash-settlements`, () => problem(409, "SETTLEMENT_EXISTS")),
    );
    const { user } = renderApp({ path: "/cash-settlements", permissions: ["cash.settle"] });

    await user.click(await screen.findByRole("button", { name: "Record settlement" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Rider/), RIDER_ID);
    await user.type(within(dialog).getByLabelText(/Received/), "100");
    await user.click(within(dialog).getByRole("button", { name: "Record settlement" }));

    await waitFor(() =>
      expect(within(dialog).getByLabelText(/^Date/)).toHaveAccessibleDescription(/already exists/i),
    );
  });
});

describe("order payments panel", () => {
  it("shows REFUND_EXCEEDS_PAYMENT at the amount field", async () => {
    const orderPayments: OrderPayments = { orderId: ORDER_ID, payments: [], refunds: [] };
    let key: string | null = null;
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}/payments`, () => HttpResponse.json(orderPayments)),
      http.post(`${API}/admin/orders/${ORDER_ID}/refunds`, ({ request }) => {
        key = request.headers.get("Idempotency-Key");
        return problem(422, "REFUND_EXCEEDS_PAYMENT");
      }),
    );
    const { user } = renderApp({
      path: "/panel",
      permissions: ["payments.view", "refunds.request"],
      routes: [{ path: "/panel", element: <OrderPaymentsPanel orderId={ORDER_ID} /> }],
    });

    await user.click(await screen.findByRole("button", { name: "Request refund" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Amount/), "900");
    await user.click(within(dialog).getByRole("combobox", { name: /Reason/ }));
    await user.click(await screen.findByRole("option", { name: "Complaint" }));
    await user.click(within(dialog).getByRole("button", { name: "Request refund" }));

    await waitFor(() =>
      expect(within(dialog).getByLabelText(/Amount/)).toHaveAccessibleDescription(
        /more than what can still be refunded/i,
      ),
    );
    expect(key).toBeTruthy();
  });
});
