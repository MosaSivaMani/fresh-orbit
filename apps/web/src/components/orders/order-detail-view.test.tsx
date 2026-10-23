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
    substitutions: [],
    payment: null,
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
});
