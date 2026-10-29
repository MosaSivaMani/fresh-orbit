import { formatRupees } from "@fresh/core";
import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { WORK_LIST_POLL_MS } from "../../api/orders";
import {
  type AdminOrderDetailResponse,
  type AdminOrderSummary,
  type ComplaintDetail,
  type CustomerOrderDetail,
  type Invoice,
} from "../../api/types/orders";
import { page } from "../../test/fixtures";
import { AdminLayout } from "../../app/AdminLayout";
import { LOCATION_A, type RenderAppOptions, renderApp as renderWith } from "../../test/render";
import { API, API_ORIGIN, problem, server } from "../../test/server";
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
    version: "11",
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

describe("orders list: new orders (AD-7)", () => {
  afterEach(() => vi.useRealTimers());

  it("re-reads the list every 30 seconds while it is open", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let calls = 0;
    server.use(
      http.get(`${API}/admin/orders`, () => {
        calls += 1;
        return HttpResponse.json(page([summary()]));
      }),
    );
    renderApp({ path: "/orders", permissions: ["orders.view"] });

    expect(await screen.findByRole("link", { name: "FO-HYD-000123" })).toBeInTheDocument();
    const before = calls;
    await vi.advanceTimersByTimeAsync(WORK_LIST_POLL_MS);
    await waitFor(() => expect(calls).toBeGreaterThan(before));
  });
});

const COMPLAINT_ID = "0192d100-0000-7000-8000-000000000001";
const PHOTO_PATH = `/api/v1/admin/complaints/${COMPLAINT_ID}/photos/0192d200-0000-7000-8000-000000000001`;

function complaint(overrides: Partial<ComplaintDetail> = {}): ComplaintDetail {
  return {
    id: COMPLAINT_ID,
    version: "3",
    orderId: ORDER_ID,
    orderNumber: "FO-HYD-000123",
    orderItemId: null,
    fulfillmentLocationId: LOCATION_A,
    kind: "DAMAGED",
    description: "The pack was torn.",
    status: "RESOLVED",
    resolution: "NONE",
    refundAmountPaise: null,
    decisionNote: null,
    raisedAt: "2026-10-24T09:00:00Z",
    decidedAt: null,
    resolvedAt: null,
    attachmentUrls: [PHOTO_PATH],
    productName: null,
    variantName: null,
    preparationName: null,
    maxRefundPaise: 0,
    photos: [],
    ...overrides,
  };
}

describe("complaint photos (AD-3)", () => {
  beforeEach(() => {
    // jsdom has no object URLs.
    URL.createObjectURL = vi.fn(() => "blob:photo-1");
    URL.revokeObjectURL = vi.fn();
  });

  it("fetches each photo with the staff token and shows it from an object URL, revoked on leave", async () => {
    let authorization: string | null = null;
    server.use(
      http.get(`${API}/admin/complaints/${COMPLAINT_ID}`, () => HttpResponse.json(complaint())),
      http.get(`${API_ORIGIN}${PHOTO_PATH}`, ({ request }) => {
        authorization = request.headers.get("Authorization");
        return new HttpResponse(new Uint8Array([137, 80, 78, 71]), {
          headers: { "Content-Type": "image/png" },
        });
      }),
    );
    const { unmount } = renderApp({
      path: `/complaints/${COMPLAINT_ID}`,
      permissions: ["complaints.manage"],
    });

    const photo = await screen.findByRole("img", { name: "Photo 1 of 1 from the customer" });
    expect(photo).toHaveAttribute("src", "blob:photo-1");
    expect(authorization).toMatch(/^Bearer /);

    unmount();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:photo-1");
  });

  it("shows a photo the server refuses as not loaded", async () => {
    server.use(
      http.get(`${API}/admin/complaints/${COMPLAINT_ID}`, () => HttpResponse.json(complaint())),
      http.get(`${API_ORIGIN}${PHOTO_PATH}`, () => problem(404, "NOT_FOUND")),
    );
    renderApp({ path: `/complaints/${COMPLAINT_ID}`, permissions: ["complaints.manage"] });

    expect(
      await screen.findByRole("img", {
        name: "Photo 1 of 1 from the customer could not be loaded",
      }),
    ).toBeInTheDocument();
    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
});

const INVOICE: Invoice = {
  id: "0192d400-0000-7000-8000-000000000001",
  orderId: ORDER_ID,
  orderNumber: "FO-HYD-000123",
  invoiceNumber: "INV-26-000042",
  issuedAt: "2026-10-24T09:30:00Z",
  sellerLegalName: "Fresh Orbit Foods Private Limited",
  sellerGstin: "36ABCDE1234F1Z5",
  lines: [
    {
      lineNo: 1,
      description: "Seer fish · Steaks · Cleaned",
      hsnCode: "0302",
      quantity: { value: 520, unit: "GRAM" },
      unitPricePaise: 120000,
      taxablePaise: 59429,
      taxRateBp: 500,
      taxPaise: 2971,
      cgstPaise: 1486,
      sgstPaise: 1485,
      totalPaise: 62400,
    },
  ],
  taxablePaise: 59429,
  taxPaise: 2971,
  totalPaise: 62400,
  pdfUrl: null,
};

describe("order invoice (AD-5)", () => {
  it("links the issued invoice from the Payment tab", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}`, () =>
        HttpResponse.json(
          orderDetail({
            status: "DELIVERED",
            invoice: {
              id: INVOICE.id,
              invoiceNumber: INVOICE.invoiceNumber,
              issuedAt: INVOICE.issuedAt,
            },
          }),
        ),
      ),
    );
    const { user } = renderApp({ path: `/orders/${ORDER_ID}`, permissions: ["orders.view"] });

    await user.click(await screen.findByRole("tab", { name: "Payment" }));
    expect(
      await screen.findByRole("link", { name: /INV-26-000042 · View and print/ }),
    ).toHaveAttribute("href", `/orders/${ORDER_ID}/invoice`);
  });

  it("shows the server's invoice lines and totals and prints it", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}/invoice`, () => HttpResponse.json(INVOICE)),
    );
    const print = vi.spyOn(window, "print").mockImplementation(() => undefined);
    const { user } = renderApp({
      path: `/orders/${ORDER_ID}/invoice`,
      permissions: ["orders.view"],
    });

    expect(
      await screen.findByRole("heading", { name: "Invoice INV-26-000042" }),
    ).toBeInTheDocument();
    const lines = screen.getByRole("table", { name: "Invoice lines" });
    expect(within(lines).getByText("Seer fish · Steaks · Cleaned")).toBeInTheDocument();
    expect(within(lines).getByText("0302")).toBeInTheDocument();
    expect(screen.getByText("36ABCDE1234F1Z5", { exact: false })).toBeInTheDocument();
    expect(screen.getAllByText(formatRupees(62400)).length).toBeGreaterThan(0);

    await user.click(screen.getByRole("button", { name: "Print" }));
    expect(print).toHaveBeenCalledTimes(1);
    print.mockRestore();
  });

  it("shows not found before the invoice is issued", async () => {
    server.use(
      http.get(`${API}/admin/orders/${ORDER_ID}/invoice`, () => problem(404, "NOT_FOUND")),
    );
    renderApp({ path: `/orders/${ORDER_ID}/invoice`, permissions: ["orders.view"] });

    expect(await screen.findByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Print" })).not.toBeInTheDocument();
  });
});
