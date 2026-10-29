import { formatRupees } from "@fresh/core";
import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type DeliverySlot, type Shipment } from "../../api/types/delivery";
import { paiseToRupeesInput } from "../../lib/units";
import { page } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const SLOT_ID = "0192b500-0001-7000-8000-000000000001";
const SHIPMENT_ID = "0192b600-0001-7000-8000-0000000000aa";

function slot(overrides: Partial<DeliverySlot> = {}): DeliverySlot {
  return {
    id: SLOT_ID,
    fulfillmentLocationId: LOCATION_A,
    slotTemplateId: null,
    slotDate: "2026-10-24",
    startAt: "2026-10-24T01:30:00Z",
    endAt: "2026-10-24T03:30:00Z",
    cutoffAt: "2026-10-23T15:30:00Z",
    capacity: 20,
    bookedCount: 6,
    status: "OPEN",
    closedReason: null,
    displayStatus: "AVAILABLE",
    version: "881",
    ...overrides,
  };
}

function shipment(overrides: Partial<Shipment> = {}): Shipment {
  return {
    id: SHIPMENT_ID,
    orderId: "0192b700-0001-7000-8000-0000000000bb",
    orderNumber: null,
    recipientName: null,
    phone: null,
    addressLine: null,
    pincode: null,
    paymentMethod: null,
    amountToCollectPaise: null,
    fulfillmentLocationId: LOCATION_A,
    deliverySlotId: SLOT_ID,
    riderId: null,
    status: "OUT_FOR_DELIVERY",
    placedAt: "2026-10-23T12:00:00Z",
    packedAt: "2026-10-24T00:45:00Z",
    coldBoxUsed: true,
    dispatchTemperatureC: 2.5,
    temperatureWarning: false,
    dispatchedAt: "2026-10-24T01:35:00Z",
    deliveredAt: null,
    returnedAt: null,
    cancelledAt: null,
    deliveryCodeFailures: 0,
    deliveryCodeLocked: false,
    cashCollectedPaise: null,
    cashMode: null,
    upiReference: null,
    cashCollectedAt: null,
    attempts: [],
    version: "5150",
    ...overrides,
  };
}

function serveBoard(shipments: Shipment[]) {
  server.use(
    http.get(`${API}/admin/delivery/shipments`, () => HttpResponse.json(page(shipments))),
    http.get(`${API}/admin/delivery/slots`, () => HttpResponse.json(page([slot()]))),
    http.get(`${API}/admin/delivery/riders`, () => HttpResponse.json(page([]))),
  );
}

describe("Shipments board", () => {
  it("delivers with the code, quoting If-Match, and shows DELIVERY_CODE_INVALID at the code field", async () => {
    serveBoard([shipment()]);
    let ifMatch: string | null = null;
    let body: unknown;
    server.use(
      http.post(`${API}/admin/delivery/shipments/${SHIPMENT_ID}/deliver`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = await request.json();
        return problem(422, "DELIVERY_CODE_INVALID", {
          detail: "The delivery code is not correct.",
        });
      }),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    await user.click(await screen.findByRole("button", { name: "Deliver" }));
    const dialog = await screen.findByRole("dialog", { name: "Deliver" });
    await user.type(within(dialog).getByLabelText(/Delivery code/), "1234");
    await user.click(within(dialog).getByRole("button", { name: "Mark delivered" }));

    expect(
      await within(dialog).findByText("The delivery code is not correct."),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/Delivery code/)).toHaveAttribute("aria-invalid", "true");
    expect(ifMatch).toBe('"5150"');
    expect(body).toEqual({
      deliveryCode: "1234",
      cashCollectedPaise: null,
      cashMode: null,
      upiReference: null,
    });
  });

  it("sends cash typed in rupees as paise", async () => {
    serveBoard([shipment()]);
    let body: unknown;
    server.use(
      http.post(`${API}/admin/delivery/shipments/${SHIPMENT_ID}/deliver`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(shipment({ status: "DELIVERED", version: "5151" }));
      }),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    await user.click(await screen.findByRole("button", { name: "Deliver" }));
    const dialog = await screen.findByRole("dialog", { name: "Deliver" });
    await user.type(within(dialog).getByLabelText(/Delivery code/), "4321");
    await user.type(within(dialog).getByLabelText(/Cash collected/), "1249.50");
    await user.click(within(dialog).getByRole("combobox", { name: /Collected as/ }));
    await user.click(await screen.findByRole("option", { name: "Cash" }));
    await user.click(within(dialog).getByRole("button", { name: "Mark delivered" }));

    expect(await screen.findByText("Delivered")).toBeInTheDocument();
    expect(body).toMatchObject({
      deliveryCode: "4321",
      cashCollectedPaise: 124950,
      cashMode: "CASH",
    });
  });
});

const COD_ORDER = {
  orderNumber: "FO-TST-000123",
  recipientName: "Ravi K",
  phone: "+919800000001",
  addressLine: "12 Lake Road, Near the park, Test Nagar, Test City",
  pincode: "500081",
  paymentMethod: "COD",
  amountToCollectPaise: 66950,
} as const;

describe("Shipments board: order facts (AD-1, AD-2)", () => {
  it("shows the order number, customer, phone, address and the server's amount to collect", async () => {
    serveBoard([shipment(COD_ORDER)]);
    renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    const table = await screen.findByRole("table", { name: "Shipments" });
    expect(await within(table).findByRole("link", { name: "FO-TST-000123" })).toHaveAttribute(
      "href",
      `/delivery/shipments/${SHIPMENT_ID}`,
    );
    expect(within(table).getByText("Ravi K")).toBeInTheDocument();
    expect(within(table).getByRole("link", { name: "+919800000001" })).toHaveAttribute(
      "href",
      "tel:+919800000001",
    );
    expect(within(table).getByText(/12 Lake Road, Near the park/)).toBeInTheDocument();
    expect(within(table).getByText("500081")).toBeInTheDocument();
    expect(
      within(table).getByText(`Collect ${formatRupees(66950)} · Cash on delivery`),
    ).toBeInTheDocument();
  });

  it("starts the cash dialog at the amount due and records it in paise with an Idempotency-Key", async () => {
    serveBoard([shipment({ ...COD_ORDER, status: "DELIVERED" })]);
    let body: unknown;
    let key: string | null = null;
    server.use(
      http.post(
        `${API}/admin/delivery/shipments/${SHIPMENT_ID}/cash-collected`,
        async ({ request }) => {
          key = request.headers.get("Idempotency-Key");
          body = await request.json();
          return HttpResponse.json({});
        },
      ),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    await user.click(await screen.findByRole("button", { name: "Record cash" }));
    const dialog = await screen.findByRole("dialog", { name: "Record cash collected" });
    expect(
      within(dialog).getByText(new RegExp(`Amount due: ${formatRupees(66950)}`)),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/Amount/)).toHaveValue(paiseToRupeesInput(66950));
    await user.click(within(dialog).getByRole("combobox", { name: /Collected as/ }));
    await user.click(await screen.findByRole("option", { name: "Cash" }));
    await user.click(within(dialog).getByRole("button", { name: "Record" }));

    await waitFor(() =>
      expect(body).toEqual({ amountPaise: 66950, mode: "CASH", upiReference: null }),
    );
    expect(key).toBeTruthy();
  });

  it("offers no cash field or Record cash for a prepaid order", async () => {
    serveBoard([shipment({ ...COD_ORDER, paymentMethod: "ONLINE", amountToCollectPaise: 0 })]);
    let body: unknown;
    server.use(
      http.post(`${API}/admin/delivery/shipments/${SHIPMENT_ID}/deliver`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(shipment({ status: "DELIVERED", version: "5151" }));
      }),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    const table = await screen.findByRole("table", { name: "Shipments" });
    expect(await within(table).findByText("Prepaid: nothing to collect")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Record cash" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Deliver" }));
    const dialog = await screen.findByRole("dialog", { name: "Deliver" });
    expect(within(dialog).queryByLabelText(/Cash collected/)).not.toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/Delivery code/), "4321");
    await user.click(within(dialog).getByRole("button", { name: "Mark delivered" }));

    await waitFor(() =>
      expect(body).toMatchObject({ deliveryCode: "4321", cashCollectedPaise: null }),
    );
  });
});

describe("Shipments board: a 412 on an action (AD-10)", () => {
  function serveConflictOnDispatch(fresh: Shipment) {
    const ifMatches: (string | null)[] = [];
    const bodies: unknown[] = [];
    server.use(
      http.get(`${API}/admin/delivery/shipments/${SHIPMENT_ID}`, () => HttpResponse.json(fresh)),
      http.post(`${API}/admin/delivery/shipments/${SHIPMENT_ID}/dispatch`, async ({ request }) => {
        ifMatches.push(request.headers.get("If-Match"));
        bodies.push(await request.json());
        return ifMatches.length === 1
          ? problem(412, "CONCURRENCY_CONFLICT")
          : HttpResponse.json({ ...fresh, status: "OUT_FOR_DELIVERY", version: "5153" });
      }),
    );
    return { ifMatches, bodies };
  }

  async function dispatchAndReload(user: ReturnType<typeof renderApp>["user"]) {
    await user.click(await screen.findByRole("button", { name: "Dispatch" }));
    const dialog = await screen.findByRole("dialog", { name: "Dispatch shipment" });
    await user.type(within(dialog).getByLabelText(/Cold-box temperature/), "2.5");
    await user.click(within(dialog).getByRole("button", { name: "Dispatch" }));
    await screen.findByRole("dialog", { name: "This changed since you opened it" });
    await user.click(screen.getByRole("button", { name: "Reload" }));
  }

  it("Reload re-reads the shipment and repeats the action with the new version", async () => {
    serveBoard([shipment({ status: "ASSIGNED", riderId: "rider-1", version: "5150" })]);
    const seen = serveConflictOnDispatch(
      shipment({ status: "ASSIGNED", riderId: "rider-2", version: "5152" }),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    await dispatchAndReload(user);

    await waitFor(() => expect(seen.ifMatches).toEqual(['"5150"', '"5152"']));
    expect(seen.bodies).toEqual([{ dispatchTemperatureC: 2.5 }, { dispatchTemperatureC: 2.5 }]);
    expect(await screen.findByText("Shipment dispatched")).toBeInTheDocument();
  });

  it("does not repeat the action when the reloaded shipment no longer offers it", async () => {
    serveBoard([shipment({ status: "ASSIGNED", riderId: "rider-1", version: "5150" })]);
    const seen = serveConflictOnDispatch(
      shipment({ status: "OUT_FOR_DELIVERY", riderId: "rider-1", version: "5152" }),
    );
    const { user } = renderApp({ path: "/delivery", permissions: ["delivery.manage"] });

    await dispatchAndReload(user);

    expect(await screen.findByText("This shipment changed")).toBeInTheDocument();
    expect(seen.ifMatches).toEqual(['"5150"']);
    expect(screen.queryByRole("dialog", { name: "Dispatch shipment" })).not.toBeInTheDocument();
  });
});

describe("Slots", () => {
  it("shows SLOT_CAPACITY_BELOW_BOOKED at the capacity field after a PATCH with If-Match", async () => {
    server.use(http.get(`${API}/admin/delivery/slots`, () => HttpResponse.json(page([slot()]))));
    let ifMatch: string | null = null;
    let body: unknown;
    server.use(
      http.patch(`${API}/admin/delivery/slots/${SLOT_ID}`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = await request.json();
        return problem(422, "SLOT_CAPACITY_BELOW_BOOKED", {
          detail: "Capacity cannot be below the 6 orders already booked.",
        });
      }),
    );
    const { user } = renderApp({ path: "/delivery/slots", permissions: ["delivery.manage"] });

    await user.click(await screen.findByRole("button", { name: "Change" }));
    const dialog = await screen.findByRole("dialog", { name: "Change slot" });
    const capacity = within(dialog).getByLabelText(/Capacity/);
    await user.clear(capacity);
    await user.type(capacity, "4");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(
      await within(dialog).findByText("Capacity cannot be below the 6 orders already booked."),
    ).toBeInTheDocument();
    expect(capacity).toHaveAttribute("aria-invalid", "true");
    expect(ifMatch).toBe('"881"');
    expect(body).toEqual({ capacity: 4 });
  });
});
