import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { type TodaysPriceItem, type TodaysPricesResponse } from "../../api/types/buying";
import { type BulkPriceResponse } from "../../api/types/pricing";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const SEA_FISH = "0192d300-0000-7000-8000-000000000001";

interface BulkCall {
  dryRun: string | null;
  key: string | null;
  body: Record<string, unknown>;
}

function pomfret(overrides: Partial<TodaysPriceItem> = {}): TodaysPriceItem {
  return {
    variantId: "variant-pomfret",
    productId: "product-pomfret",
    productName: "Silver Pomfret",
    variantName: "Pomfret per kg",
    sku: "POMF-KG",
    categoryId: SEA_FISH,
    categoryName: "Sea fish",
    sellUnit: "KG",
    stockUnit: "GRAM",
    packSizeGrams: null,
    unitPricePaise: 90000,
    compareAtPaise: null,
    priceValidFrom: "2026-11-01T00:00:00Z",
    nextUnitPricePaise: null,
    nextValidFrom: null,
    taxRateBp: 500,
    preparationCode: "CURRY_CUT",
    yieldBp: 6500,
    latestBatchId: "batch-1",
    latestBatchReceivedAt: "2026-11-05T01:00:00Z",
    latestBatchCostPaise: 455000,
    latestBatchQuantity: 10000,
    costPerUnitPaise: 70000,
    marginBp: 1833,
    targetMarginBp: 2500,
    suggestedPricePaise: 98100,
    issues: [],
    ...overrides,
  };
}

function lobster(): TodaysPriceItem {
  return pomfret({
    variantId: "variant-lobster",
    productName: "Rock Lobster",
    variantName: "Lobster per piece",
    sku: "LOBSTER-PC",
    categoryId: null,
    categoryName: null,
    sellUnit: "PIECE",
    stockUnit: "PIECE",
    unitPricePaise: null,
    taxRateBp: null,
    latestBatchId: null,
    latestBatchReceivedAt: null,
    latestBatchCostPaise: null,
    latestBatchQuantity: null,
    costPerUnitPaise: null,
    marginBp: null,
    targetMarginBp: null,
    suggestedPricePaise: null,
    issues: ["PRICE_MISSING", "BATCH_MISSING", "TAX_RULE_MISSING"],
  });
}

function prices(): TodaysPricesResponse {
  return {
    asOf: "2026-11-05T12:00:00Z",
    fulfillmentLocationId: LOCATION_A,
    locationCode: "L3",
    locationName: "Harbour Hub",
    timeZone: "Asia/Kolkata",
    tomorrowStartsAt: "2026-11-05T18:30:00Z",
    targetMarginBp: null,
    categoryTargets: {},
    items: [pomfret(), lobster()],
    page: 1,
    pageSize: 2,
    totalCount: 2,
  };
}

function bulkResult(dryRun: boolean): BulkPriceResponse {
  return {
    uploadId: dryRun ? null : "upload-1",
    dryRun,
    rowCount: 1,
    changedCount: 1,
    unchangedCount: 0,
    rows: [
      {
        index: 0,
        kind: "PRICE",
        locationCode: "L3",
        sku: "POMF-KG",
        preparationCode: null,
        status: "CHANGED",
        current: {
          unitPricePaise: 90000,
          compareAtPaise: null,
          chargePaise: null,
          chargeBasis: null,
        },
        new: {
          unitPricePaise: 98100,
          compareAtPaise: null,
          chargePaise: null,
          chargeBasis: null,
        },
        validFrom: "2026-11-05T18:30:00Z",
        changeBp: 900,
        largeChange: true,
      },
    ],
  };
}

describe("Today's prices", () => {
  it("renders the server's cost, margin and suggestion and sends the typed targets", async () => {
    const queries: URLSearchParams[] = [];
    server.use(
      http.get(`${API}/admin/reports/todays-prices`, ({ request }) => {
        queries.push(new URL(request.url).searchParams);
        return HttpResponse.json(prices());
      }),
    );
    const { user } = renderApp({ path: "/pricing/today", permissions: ["pricing.manage"] });

    const row = await screen.findByTestId("price-POMF-KG");
    expect(queries[0]?.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(within(row).getByText("₹900")).toBeInTheDocument();
    expect(within(row).getByText("₹700")).toBeInTheDocument();
    expect(within(row).getByText("18.33%")).toBeInTheDocument();
    expect(within(row).getByText("₹981")).toBeInTheDocument();
    const lobsterRow = screen.getByTestId("price-LOBSTER-PC");
    expect(within(lobsterRow).getByText("No price here")).toBeInTheDocument();
    expect(within(lobsterRow).getByText("No GST rule")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Target margin (%)"), "30");
    await user.type(screen.getByLabelText("Sea fish (%)"), "25");
    const last = () => queries[queries.length - 1];
    await vi.waitFor(() => expect(last()?.get("targetMarginBp")).toBe("3000"));
    await vi.waitFor(() => expect(last()?.get("categoryTargets")).toBe(`${SEA_FISH}:2500`));
  });

  it("uses a suggestion only on request, reviews with a dry run, then saves from tomorrow", async () => {
    const calls: BulkCall[] = [];
    server.use(
      http.get(`${API}/admin/reports/todays-prices`, () => HttpResponse.json(prices())),
      http.post(`${API}/admin/pricing/prices/bulk`, async ({ request }) => {
        const dryRun = new URL(request.url).searchParams.get("dryRun");
        const body = (await request.json()) as Record<string, unknown>;
        calls.push({ dryRun, key: request.headers.get("Idempotency-Key"), body });
        return HttpResponse.json(bulkResult(dryRun === "true"));
      }),
    );
    const { user } = renderApp({ path: "/pricing/today", permissions: ["pricing.manage"] });
    await screen.findByTestId("price-POMF-KG");

    const input = screen.getByLabelText("New price for POMF-KG");
    expect(input).toHaveValue("");
    expect(screen.getByRole("button", { name: "Save prices" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Use the suggested price for POMF-KG" }));
    expect(input).toHaveValue("981.00");
    expect(calls).toHaveLength(0);

    await user.click(screen.getByRole("radio", { name: /Tomorrow/ }));
    await user.click(screen.getByRole("button", { name: "Review 1 change" }));

    expect(await screen.findByText("Large change")).toBeInTheDocument();
    expect(calls[0]?.dryRun).toBe("true");
    expect(calls[0]?.body).toMatchObject({
      validFrom: "2026-11-05T18:30:00Z",
      replaceFuture: false,
      rows: [{ kind: "PRICE", locationCode: "L3", sku: "POMF-KG", unitPricePaise: 98100 }],
    });

    await user.click(screen.getByRole("button", { name: "Save prices" }));
    expect(await screen.findByText("Prices saved")).toBeInTheDocument();
    expect(calls[1]?.dryRun).not.toBe("true");
    expect(calls[1]?.body).toEqual(calls[0]?.body);
    expect(calls[1]?.key).toBeTruthy();
    expect(calls[1]?.key).not.toBe(calls[0]?.key);
  });

  it("shows a refused row at its variant and saves nothing", async () => {
    server.use(
      http.get(`${API}/admin/reports/todays-prices`, () => HttpResponse.json(prices())),
      http.post(`${API}/admin/pricing/prices/bulk`, () =>
        problem(422, "VALIDATION_FAILED", {
          errors: {
            "rows[0].unitPricePaise": [
              { code: "VALIDATION_FAILED", message: "Unit price must be at most ₹50,000." },
            ],
          },
        }),
      ),
    );
    const { user } = renderApp({ path: "/pricing/today", permissions: ["pricing.manage"] });
    const row = await screen.findByTestId("price-POMF-KG");

    await user.type(within(row).getByLabelText("New price for POMF-KG"), "60000");
    await user.click(screen.getByRole("button", { name: "Review 1 change" }));

    const refusal = await within(row).findByText("Unit price must be at most ₹50,000.");
    expect(refusal).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save prices" })).toBeDisabled();
  });
});
