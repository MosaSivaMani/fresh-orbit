import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type StockItem } from "../../api/types/inventory";
import { page } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const VARIANT = "0192b500-0000-7000-8000-00000000a001";
const STOCK_ITEM = "0192b600-0000-7000-8000-00000000b001";

function stockItem(overrides: Partial<StockItem> = {}): StockItem {
  return {
    id: STOCK_ITEM,
    version: "4711",
    fulfillmentLocationId: LOCATION_A,
    variantId: VARIANT,
    stockUnit: "GRAM",
    onHand: { value: 12000, unit: "GRAM" },
    reserved: { value: 2000, unit: "GRAM" },
    available: { value: 10000, unit: "GRAM" },
    lowStockThreshold: 15000,
    isLowStock: true,
    batches: null,
    productName: "Seer fish",
    variantName: "Steaks",
    sku: "SEER-STEAK",
    ...overrides,
  };
}

describe("Stock list", () => {
  it("renders stock rows and puts the low-stock filter in the URL and the request", async () => {
    const requests: URL[] = [];
    server.use(
      http.get(`${API}/admin/inventory/stock`, ({ request }) => {
        requests.push(new URL(request.url));
        return HttpResponse.json(page([stockItem()]));
      }),
    );
    const { user, router } = renderApp({ path: "/inventory", permissions: ["inventory.view"] });

    const table = await screen.findByRole("table", { name: "Stock items" });
    expect(await within(table).findByText("Low stock")).toBeInTheDocument();
    expect(within(table).getByText("SEER-STEAK").parentElement).toHaveTextContent(
      "Seer fish · Steaks",
    );
    expect(requests[0]?.searchParams.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(requests[0]?.searchParams.has("lowStockOnly")).toBe(false);

    await user.click(screen.getByRole("combobox", { name: "Stock level" }));
    await user.click(await screen.findByRole("option", { name: "Low stock only" }));

    await waitFor(() => expect(router.state.location.search).toContain("lowStockOnly=true"));
    await waitFor(() => expect(requests.at(-1)?.searchParams.get("lowStockOnly")).toBe("true"));
  });
});

describe("Receive stock", () => {
  it("sends an Idempotency-Key with the receipt and shows a server field error at the field", async () => {
    let body: Record<string, unknown> | undefined;
    let key: string | null = null;
    server.use(
      http.get(`${API}/admin/catalog/variants`, () =>
        HttpResponse.json(
          page([
            {
              variantId: VARIANT,
              productId: "0192b400-0000-7000-8000-00000000c001",
              productName: "Seer fish",
              variantName: "Steaks",
              sku: "SEER-STEAK",
              sellUnit: "KG",
              stockUnit: "GRAM",
              productStatus: "ACTIVE",
              isActive: true,
            },
          ]),
        ),
      ),
      http.post(`${API}/admin/inventory/receipts`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        key = request.headers.get("Idempotency-Key");
        return problem(422, "VALIDATION_FAILED", {
          errors: {
            batchCode: [
              { code: "DUPLICATE_BATCH_CODE", message: "Batch code is already used here." },
            ],
          },
        });
      }),
    );
    const { user } = renderApp({
      path: "/inventory/receive",
      permissions: ["inventory.view", "inventory.receive"],
    });

    // Any staff member picks the variant by name or SKU (G3), without the catalogue permission.
    await user.click(await screen.findByRole("combobox", { name: /^Variant/ }));
    await user.keyboard("SEER-STEAK");
    const option = "Seer fish · Steaks (SEER-STEAK)";
    await user.click(await screen.findByRole("option", { name: option }));
    await user.type(screen.getByLabelText(/Batch code/), "KAS-2410-01");
    await user.type(screen.getByLabelText(/Received quantity/), "25000");
    await user.type(screen.getByLabelText(/Total cost/), "12500.50");
    fireEvent.change(screen.getByLabelText(/Best before/), {
      target: { value: "2026-10-27T18:00" },
    });
    await user.type(screen.getByLabelText(/Temperature on receipt/), "2.5");
    await user.click(screen.getByRole("button", { name: "Receive stock" }));

    const batchCode = screen.getByLabelText(/Batch code/);
    await waitFor(() => expect(batchCode).toHaveAttribute("aria-invalid", "true"));
    expect(screen.getByText("Batch code is already used here.")).toBeInTheDocument();
    expect(key).toBeTruthy();
    expect(body).toMatchObject({
      fulfillmentLocationId: LOCATION_A,
      variantId: VARIANT,
      batchCode: "KAS-2410-01",
      receivedQuantity: { value: 25000, unit: "GRAM" },
      totalCostPaise: 1250050,
      bestBeforeAt: "2026-10-27T12:30:00.000Z",
      receiptTemperatureC: 2.5,
    });
  });
});

describe("Stock adjustment", () => {
  it("quotes the stock item version and shows the conflict prompt on 412", async () => {
    let ifMatch: string | null = null;
    let body: Record<string, unknown> | undefined;
    server.use(
      http.get(`${API}/admin/inventory/stock/${STOCK_ITEM}`, () =>
        HttpResponse.json(stockItem({ batches: [] })),
      ),
      http.post(`${API}/admin/inventory/adjustments`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = (await request.json()) as Record<string, unknown>;
        return problem(412, "CONCURRENCY_CONFLICT");
      }),
    );
    const { user } = renderApp({
      path: `/inventory/stock/${STOCK_ITEM}`,
      permissions: ["inventory.view", "inventory.adjust"],
    });

    await user.click(await screen.findByRole("button", { name: "Adjust" }));
    const dialog = await screen.findByRole("dialog", { name: "Adjust stock" });
    await user.type(within(dialog).getByLabelText(/Quantity/), "500");
    await user.click(within(dialog).getByRole("combobox", { name: /Reason/ }));
    await user.click(await screen.findByRole("option", { name: "Found" }));
    await user.click(within(dialog).getByRole("button", { name: "Record adjustment" }));

    expect(await screen.findByText("This changed since you opened it")).toBeInTheDocument();
    expect(ifMatch).toContain("4711");
    expect(body).toMatchObject({
      stockItemId: STOCK_ITEM,
      kind: "ADJUSTMENT_IN",
      quantity: { value: 500, unit: "GRAM" },
      reasonCode: "FOUND",
    });
  });
});
