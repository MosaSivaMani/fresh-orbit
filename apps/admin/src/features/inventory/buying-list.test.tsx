import { fireEvent, screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { type BuyingListItem, type BuyingListResponse } from "../../api/types/buying";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, server } from "../../test/server";

function pomfret(overrides: Partial<BuyingListItem> = {}): BuyingListItem {
  return {
    variantId: "variant-pomfret",
    productId: "product-pomfret",
    productName: "Silver Pomfret",
    variantName: "Pomfret per kg",
    sku: "POMF-KG",
    sellUnit: "KG",
    stockUnit: "GRAM",
    packSizeGrams: null,
    ordersCount: 2,
    orderedQuantity: 2500,
    orderedNetGrams: 2500,
    neededQuantity: 3309,
    estimatedRawGrams: 3309,
    withMarginQuantity: 3640,
    withMarginRawGrams: 3640,
    onHand: 5300,
    reservedForOtherOrders: 1924,
    availableQuantity: 3376,
    shortfallQuantity: 264,
    shortfallRawGrams: 264,
    stockItemMissing: false,
    preparations: [
      {
        preparationCode: "CURRY_CUT",
        preparationName: "Curry cut",
        yieldBp: 6500,
        ordersCount: 2,
        orderedQuantity: 1500,
        orderedNetGrams: 1500,
        neededQuantity: 2309,
        estimatedRawGrams: 2309,
      },
    ],
    ...overrides,
  };
}

function seerPacks(): BuyingListItem {
  return pomfret({
    variantId: "variant-seer",
    productName: "Seer Fish Steaks",
    variantName: "Steaks 500 g net",
    sku: "SEER-STEAK-500",
    sellUnit: "PACK",
    stockUnit: "PIECE",
    packSizeGrams: 500,
    ordersCount: 1,
    orderedQuantity: 2,
    orderedNetGrams: 1000,
    neededQuantity: 2,
    estimatedRawGrams: 1429,
    withMarginQuantity: 3,
    withMarginRawGrams: 1572,
    onHand: null,
    reservedForOtherOrders: 0,
    availableQuantity: 0,
    shortfallQuantity: 3,
    shortfallRawGrams: 1572,
    stockItemMissing: true,
    preparations: [],
  });
}

function list(overrides: Partial<BuyingListResponse> = {}): BuyingListResponse {
  return {
    asOf: "2026-11-05T20:00:00Z",
    fulfillmentLocationId: LOCATION_A,
    locationCode: "L3",
    locationName: "Harbour Hub",
    timeZone: "Asia/Kolkata",
    deliveryDate: "2026-11-06",
    safetyMarginBp: 1000,
    ordersCount: 2,
    pendingPaymentOrders: 1,
    estimatedRawGrams: 5212,
    shortfallRawGrams: 1836,
    items: [seerPacks(), pomfret()],
    page: 1,
    pageSize: 2,
    totalCount: 2,
    ...overrides,
  };
}

describe("Buying list", () => {
  // jsdom has no object URLs; the CSV download saves a Blob through one.
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:buying-list");
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
  });

  it("sends the location, date and typed margin and renders the server's figures", async () => {
    const queries: URLSearchParams[] = [];
    server.use(
      http.get(`${API}/admin/reports/buying-list`, ({ request }) => {
        queries.push(new URL(request.url).searchParams);
        return HttpResponse.json(list());
      }),
    );
    const { user } = renderApp({ path: "/inventory/buying-list", permissions: ["inventory.view"] });

    const pomfretRow = await screen.findByTestId("buy-POMF-KG");
    expect(queries[0]?.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(queries[0]?.has("safetyMarginBp")).toBe(false);
    const curry = within(pomfretRow).getByText(/^Curry cut: 1.5 kg/);
    expect(curry).toHaveTextContent("yield 65% → 2.309 kg raw");
    expect(within(pomfretRow).getByText("3.64 kg")).toBeInTheDocument();
    expect(within(pomfretRow).getByText("264 g")).toBeInTheDocument();
    expect(screen.getByText(/1 online order is still waiting for payment/)).toBeInTheDocument();

    const packs = screen.getByTestId("buy-SEER-STEAK-500");
    expect(within(packs).getByText("No stock item")).toBeInTheDocument();
    expect(within(packs).getAllByText("3 packs")).toHaveLength(2);
    expect(within(packs).getAllByText("1.572 kg raw")).toHaveLength(2);

    await user.type(screen.getByLabelText(/Safety margin/), "10");
    fireEvent.change(screen.getByLabelText(/Delivery date/), { target: { value: "2026-11-06" } });
    const last = () => queries[queries.length - 1];
    await vi.waitFor(() => expect(last()?.get("safetyMarginBp")).toBe("1000"));
    await vi.waitFor(() => expect(last()?.get("deliveryDate")).toBe("2026-11-06"));
  });

  it("refuses a margin it cannot read instead of sending it", async () => {
    let calls = 0;
    server.use(
      http.get(`${API}/admin/reports/buying-list`, () => {
        calls += 1;
        return HttpResponse.json(list());
      }),
    );
    const { user } = renderApp({ path: "/inventory/buying-list", permissions: ["inventory.view"] });
    await screen.findByTestId("buy-POMF-KG");

    await user.type(screen.getByLabelText(/Safety margin/), "abc");

    expect(await screen.findByText(/between 0 and 100 %/)).toBeInTheDocument();
    expect(calls).toBe(1);
  });

  it("downloads the CSV and prints only the list", async () => {
    let format: string | null = null;
    server.use(
      http.get(`${API}/admin/reports/buying-list`, ({ request }) => {
        format = new URL(request.url).searchParams.get("format");
        if (format === "csv") {
          return new HttpResponse("row_type,product_name\r\n", {
            headers: {
              "Content-Type": "text/csv; charset=utf-8",
              "Content-Disposition": 'attachment; filename="buying_list_L3_2026-11-06.csv"',
            },
          });
        }
        return HttpResponse.json(list());
      }),
    );
    const print = vi.spyOn(window, "print").mockImplementation(() => undefined);
    const { user } = renderApp({ path: "/inventory/buying-list", permissions: ["inventory.view"] });
    await screen.findByTestId("buy-POMF-KG");

    await user.click(screen.getByRole("button", { name: "Download CSV" }));
    await vi.waitFor(() => expect(format).toBe("csv"));
    await vi.waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: "Print" }));
    expect(print).toHaveBeenCalledTimes(1);
    print.mockRestore();
  });
});
