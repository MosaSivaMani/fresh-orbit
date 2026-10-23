import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { type PriceListItem, type PriceSchedule } from "../../api/types/pricing";
import { page } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const VARIANT_ID = "0192d200-0000-7000-8000-000000000001";
const PRICE_PATH = `${API}/admin/pricing/locations/${LOCATION_A}/variants/${VARIANT_ID}/price`;

function item(overrides: Partial<PriceListItem> = {}): PriceListItem {
  return {
    fulfillmentLocationId: LOCATION_A,
    variantId: VARIANT_ID,
    productId: "product-1",
    sku: "SEER-STEAK",
    productName: "Seer fish",
    variantName: "Steaks",
    priceUnit: "KG",
    current: null,
    next: null,
    missing: true,
    ...overrides,
  };
}

function schedule(overrides: Partial<PriceSchedule> = {}): PriceSchedule {
  return {
    fulfillmentLocationId: LOCATION_A,
    variantId: VARIANT_ID,
    version: null,
    current: null,
    next: null,
    history: [],
    unchanged: false,
    largeChange: false,
    ...overrides,
  };
}

const PERIOD = {
  id: "period-1",
  unitPricePaise: 120000,
  compareAtPaise: null,
  validFrom: "2026-10-01T00:00:00Z",
  validTo: null,
};

async function setPrice(user: ReturnType<typeof renderApp>["user"], rupees: string) {
  await user.click(await screen.findByRole("button", { name: "Set price for SEER-STEAK" }));
  const price = await screen.findByLabelText(/^Price \(₹\)/);
  await user.type(price, rupees);
  await user.click(screen.getByRole("button", { name: "Save price" }));
}

describe("pricing", () => {
  it("sets a first price with If-None-Match: * and sends paise", async () => {
    let headers: Headers | undefined;
    let body: Record<string, unknown> | undefined;
    let query: URLSearchParams | undefined;
    server.use(
      http.get(`${API}/admin/pricing/prices`, ({ request }) => {
        query = new URL(request.url).searchParams;
        return HttpResponse.json(page([item()]));
      }),
      http.get(PRICE_PATH, () => HttpResponse.json(schedule())),
      http.put(PRICE_PATH, async ({ request }) => {
        headers = request.headers;
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(schedule({ version: "7", current: PERIOD }), {
          headers: { ETag: '"7"' },
        });
      }),
    );
    const { user } = renderApp({ path: "/pricing", permissions: ["pricing.manage"] });

    await setPrice(user, "1249.50");

    expect(await screen.findByText("Price saved")).toBeInTheDocument();
    expect(query?.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(headers?.get("If-None-Match")).toBe("*");
    expect(headers?.get("If-Match")).toBeNull();
    expect(body).toMatchObject({ unitPricePaise: 124950, compareAtPaise: null, validFrom: null });
  });

  it("changes an existing price with If-Match and shows PRICE_PERIOD_OVERLAP at replace-future", async () => {
    let headers: Headers | undefined;
    server.use(
      http.get(`${API}/admin/pricing/prices`, () =>
        HttpResponse.json(page([item({ current: PERIOD, missing: false })])),
      ),
      http.get(PRICE_PATH, () => HttpResponse.json(schedule({ version: "6", current: PERIOD }))),
      http.put(PRICE_PATH, ({ request }) => {
        headers = request.headers;
        return problem(409, "PRICE_PERIOD_OVERLAP", {
          errors: {
            validFrom: [
              {
                code: "PRICE_PERIOD_OVERLAP",
                message:
                  "A future period already starts after this time; send replaceFuture=true to replace it.",
              },
            ],
          },
        });
      }),
    );
    const { user } = renderApp({ path: "/pricing", permissions: ["pricing.manage"] });

    await setPrice(user, "1300");

    expect(await screen.findByText(/Tick this to replace it/)).toBeInTheDocument();
    expect(headers?.get("If-Match")).toBe('"6"');
    expect(headers?.get("If-None-Match")).toBeNull();
  });

  it("asks for a location when the switcher is on all locations", async () => {
    renderApp({
      path: "/pricing",
      permissions: ["pricing.manage", "locations.manage"],
      allLocations: true,
    });
    expect(await screen.findByText("Choose a location")).toBeInTheDocument();
  });

  it("bulk upload: dry run shows row errors without applying, then apply sends its own Idempotency-Key", async () => {
    const calls: { dryRun: string | null; key: string | null; contentType: string | null }[] = [];
    let attempt = 0;
    server.use(
      http.post(`${API}/admin/pricing/prices/bulk`, ({ request }) => {
        const url = new URL(request.url);
        calls.push({
          dryRun: url.searchParams.get("dryRun"),
          key: request.headers.get("Idempotency-Key"),
          contentType: request.headers.get("Content-Type"),
        });
        attempt += 1;
        if (attempt === 1) {
          return problem(422, "VALIDATION_FAILED", {
            errors: {
              "rows[1].sku": [{ code: "SKU_UNKNOWN", message: "No variant has this SKU." }],
            },
          });
        }
        const dryRun = url.searchParams.get("dryRun") === "true";
        return HttpResponse.json({
          uploadId: dryRun ? null : "upload-1",
          dryRun,
          rowCount: 1,
          changedCount: 1,
          unchangedCount: 0,
          rows: [
            {
              index: 0,
              kind: "PRICE",
              locationCode: "CHN-01",
              sku: "SEER-STEAK",
              preparationCode: null,
              status: "CHANGED",
              current: {
                unitPricePaise: 120000,
                compareAtPaise: null,
                chargePaise: null,
                chargeBasis: null,
              },
              new: {
                unitPricePaise: 124900,
                compareAtPaise: null,
                chargePaise: null,
                chargeBasis: null,
              },
              validFrom: "2026-10-24T00:00:00Z",
              changeBp: 408,
              largeChange: false,
            },
          ],
        });
      }),
    );
    const { user } = renderApp({ path: "/pricing/bulk", permissions: ["pricing.manage"] });

    const rows = await screen.findByLabelText(/Rows \(CSV\)/);
    await user.click(rows);
    await user.paste(
      "kind,location_code,sku,unit_price_paise\nPRICE,CHN-01,SEER-STEAK,124900\nPRICE,CHN-01,NOPE,1000",
    );
    await user.click(screen.getByRole("button", { name: "Dry run" }));

    const errors = await screen.findByRole("table", { name: "Row errors" });
    expect(within(errors).getByText("No variant has this SKU.")).toBeInTheDocument();
    expect(within(errors).getByText("sku")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Apply" })).toBeDisabled();
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ dryRun: "true", contentType: "text/csv" });

    await user.clear(rows);
    await user.paste("kind,location_code,sku,unit_price_paise\nPRICE,CHN-01,SEER-STEAK,124900");
    await user.click(screen.getByRole("button", { name: "Dry run" }));
    expect(await screen.findByRole("table", { name: "Dry run result" })).toBeInTheDocument();
    expect(calls.every((c) => c.dryRun === "true")).toBe(true);

    await user.click(screen.getByRole("button", { name: "Apply" }));
    expect(await screen.findByRole("table", { name: "Applied rows" })).toBeInTheDocument();

    const apply = calls[2];
    expect(apply?.dryRun).not.toBe("true");
    expect(apply?.key).toBeTruthy();
    expect(new Set(calls.map((c) => c.key)).size).toBe(3);
  });
});
