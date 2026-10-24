import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import {
  type AdminOrderDetailResponse,
  type AdminOrderSummary,
  type CustomerOrderDetail,
} from "../../api/types/orders";
import { page } from "../../test/fixtures";
import { AdminLayout } from "../../app/AdminLayout";
import { LOCATION_A, type RenderAppOptions, renderApp as renderWith } from "../../test/render";
import { API, problem, server } from "../../test/server";
import { ordersRoutes } from "./routes";

// Only the shell and the orders routes, so these tests do not load other features.
vi.mock("../../app/router", () => ({ routes: [] }));

const renderApp = (options: RenderAppOptions) =>
  renderWith({
    ...options,
    routes: [{ path: "/", element: <AdminLayout />, children: ordersRoutes }],
  });

const ORDER_ID = "0192d000-0000-7000-8000-000000000010";

function totals(totalPaise: number) {
  return {
    itemsPaise: totalPaise,
    preparationPaise: 0,
    discountPaise: 0,
    deliveryFeePaise: 0,
    taxPaise: 0,
    totalPaise,
  };
}

/** `GET /admin/orders/{id}` as the API sends it: the customer order under `order`, staff extras beside it. */
function orderDetail(overrides: Partial<CustomerOrderDetail> = {}): AdminOrderDetailResponse {
  return {
    order: customerOrder(overrides),
    customerId: "cust-1",
    cityId: "city-1",
    fulfillmentLocationId: LOCATION_A,
    serviceAreaId: "area-1",
    deliverySlotId: "slot-1",
    cancelReasonCode: null,
    coldBoxUsed: false,
    refundDuePaise: 0,
    lines: [],
    history: [],
    adjustments: [],
  };
}

function customerOrder(overrides: Partial<CustomerOrderDetail>): CustomerOrderDetail {
  return {
    id: ORDER_ID,
    orderNumber: "FO-HYD-000123",
    version: "11",
    status: "PREPARING",
    paymentMethod: "COD",
    placedAt: "2026-10-24T04:00:00Z",
    customerNotes: null,
    slot: { startAt: "2026-10-24T06:30:00Z", endAt: "2026-10-24T08:30:00Z" },
    address: {
      recipientName: "Ravi K",
      phone: "+919800000001",
      line1: "12 Lake Road",
      line2: null,
      landmark: null,
      locality: "Madhapur",
      cityName: "Hyderabad",
      stateCode: "TS",
      pincode: "500081",
    },
    substitutionPreference: "CALL_ME",
    items: [
      {
        id: "item-1",
        lineNo: 1,
        variantId: "variant-1",
        productName: "Seer fish",
        variantName: "Steaks",
        preparationName: "Cleaned",
        imageKey: null,
        orderedQuantity: { value: 500, unit: "GRAM" },
        maxChargeableQuantity: { value: 550, unit: "GRAM" },
        packedQuantity: null,
        chargedQuantity: null,
        unitPricePaise: 120000,
        preparationChargePaise: 0,
        estimatedTotalPaise: 60000,
        finalTotalPaise: null,
        status: "ACTIVE",
        substitutedFromItemId: null,
      },
    ],
    estimate: totals(60000),
    final: null,
    amountDuePaise: 60000,
    amountPaidPaise: 0,
    amountRefundedPaise: 0,
    couponCode: null,
    substitutions: [],
    payment: { method: "COD", status: "PENDING", nextAction: null },
    invoice: null,
    allowedActions: { cancel: true, raiseComplaint: false, downloadInvoice: false, reorder: false },
    ...overrides,
  };
}

function summary(overrides: Partial<AdminOrderSummary> = {}): AdminOrderSummary {
  return {
    id: ORDER_ID,
    orderNumber: "FO-HYD-000123",
    status: "CONFIRMED",
    placedAt: "2026-10-24T04:00:00Z",
    fulfillmentLocationId: LOCATION_A,
    customerId: "cust-1",
    recipientName: "Ravi K",
    phone: "+919800000001",
    deliverySlotId: "slot-1",
    slotStartAt: "2026-10-24T06:30:00Z",
    slotEndAt: "2026-10-24T08:30:00Z",
    itemCount: 2,
    paymentMethod: "COD",
    totalPaise: 60000,
    finalTotalPaise: null,
    amountDuePaise: 60000,
    ...overrides,
  };
}

describe("orders list", () => {
  it("sends filters from the URL and the chosen location to the API", async () => {
    let seen: URL | null = null;
    server.use(
      http.get(`${API}/admin/orders`, ({ request }) => {
        seen = new URL(request.url);
        return HttpResponse.json(page([summary()]));
      }),
    );
    renderApp({
      path: "/orders?status=CONFIRMED&slotDate=2026-10-24",
      permissions: ["orders.view"],
    });

    expect(await screen.findByRole("link", { name: "FO-HYD-000123" })).toBeInTheDocument();
    await waitFor(() => expect(seen).not.toBeNull());
    const url = seen as unknown as URL;
    expect(url.searchParams.getAll("status")).toEqual(["CONFIRMED"]);
    expect(url.searchParams.get("slotDate")).toBe("2026-10-24");
    expect(url.searchParams.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(url.searchParams.get("sort")).toBe("-placedAt");
  });
});

describe("order detail: weigh and pack", () => {
  it("sends every active line with If-Match and shows the server's final amounts", async () => {
    let ifMatch: string | null = null;
    let body: unknown = null;
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () => HttpResponse.json(orderDetail())),
      http.post(`${API}/admin/orders/${ORDER_ID}/pack`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = await request.json();
        return HttpResponse.json({
          orderId: ORDER_ID,
          status: "PACKED",
          version: "12",
          lines: [
            {
              orderItemId: "item-1",
              packed: { value: 600, unit: "GRAM" },
              charged: { value: 550, unit: "GRAM" },
              capped: true,
              finalTotalPaise: 66000,
              adjustmentPaise: 6000,
              status: "ACTIVE",
            },
          ],
          finalTotalPaise: 66000,
          amountDuePaise: 66000,
          refundDuePaise: 0,
        });
      }),
    );
    const { user } = renderApp({
      path: `/orders/${ORDER_ID}`,
      permissions: ["orders.view", "orders.pack"],
    });

    const input = await screen.findByLabelText(/Weighed quantity for line 1/);
    await user.type(input, "600");
    await user.click(screen.getByRole("button", { name: "Mark packed" }));

    expect(await screen.findByText("Capped at max")).toBeInTheDocument();
    expect(ifMatch).toBe('"11"');
    expect(body).toEqual({
      coldBoxUsed: true,
      lines: [{ orderItemId: "item-1", packedQuantity: { value: 600, unit: "GRAM" } }],
    });
  });

  it("shows a line's field error from the server at that line", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () => HttpResponse.json(orderDetail())),
      http.post(`${API}/admin/orders/${ORDER_ID}/pack`, () =>
        problem(422, "VALIDATION_FAILED", {
          errors: {
            "lines[0].packedQuantity.value": [
              { code: "OUT_OF_RANGE", message: "Enter a packed weight below 5 kg." },
            ],
          },
        }),
      ),
    );
    const { user } = renderApp({
      path: `/orders/${ORDER_ID}`,
      permissions: ["orders.view", "orders.pack"],
    });

    await user.type(await screen.findByLabelText(/Weighed quantity for line 1/), "9000");
    await user.click(screen.getByRole("button", { name: "Mark packed" }));

    const input = screen.getByLabelText(/Weighed quantity for line 1/);
    await waitFor(() => expect(input).toHaveAttribute("aria-invalid", "true"));
  });

  it("asks to reload when the order changed since it was opened (412)", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () => HttpResponse.json(orderDetail())),
      http.post(`${API}/admin/orders/${ORDER_ID}/pack`, () => problem(412, "CONCURRENCY_CONFLICT")),
    );
    const { user } = renderApp({
      path: `/orders/${ORDER_ID}`,
      permissions: ["orders.view", "orders.pack"],
    });

    await user.type(await screen.findByLabelText(/Weighed quantity for line 1/), "500");
    await user.click(screen.getByRole("button", { name: "Mark packed" }));

    const dialog = await screen.findByRole("dialog", { name: "This changed since you opened it" });
    expect(within(dialog).getByRole("button", { name: "Reload" })).toBeInTheDocument();
  });
});

describe("order detail: cancel", () => {
  it("requires a reason, then posts it with If-Match", async () => {
    let ifMatch: string | null = null;
    let body: unknown = null;
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () =>
        HttpResponse.json(orderDetail({ status: "CONFIRMED" })),
      ),
      http.post(`${API}/admin/orders/${ORDER_ID}/cancel`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = await request.json();
        return HttpResponse.json(orderDetail({ status: "CANCELLED", version: "12" }));
      }),
    );
    const { user } = renderApp({
      path: `/orders/${ORDER_ID}`,
      permissions: ["orders.view", "orders.cancel"],
    });

    await user.click(await screen.findByRole("button", { name: "Cancel order" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Cancel order" }));
    expect(
      await within(dialog).findByText("Choose why the order is cancelled."),
    ).toBeInTheDocument();
    expect(body).toBeNull();

    await user.click(within(dialog).getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: "Out of stock" }));
    await user.click(within(dialog).getByRole("button", { name: "Cancel order" }));

    await waitFor(() => expect(body).toEqual({ reasonCode: "OUT_OF_STOCK", note: null }));
    expect(ifMatch).toBe('"11"');
  });

  it("hides actions the staff member may not take", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () =>
        HttpResponse.json(orderDetail({ status: "CONFIRMED" })),
      ),
    );
    renderApp({ path: `/orders/${ORDER_ID}`, permissions: ["orders.view"] });

    expect(await screen.findByRole("heading", { name: "FO-HYD-000123" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel order" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Start preparing" })).not.toBeInTheDocument();
  });
});
