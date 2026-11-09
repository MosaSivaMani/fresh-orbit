import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type LocationCostRates } from "../../api/types/delivery";
import { type FulfillmentLocation } from "../../api/types/locations";
import { page } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const COSTS_URL = `${API}/admin/delivery/locations/${LOCATION_A}/cost-rates`;

const LOCATION: FulfillmentLocation = {
  id: LOCATION_A,
  cityId: "0192b400-0001-7000-8000-000000000001",
  code: "TST_NORTH",
  name: "Test North",
  addressLine: null,
  pincode: null,
  phone: null,
  latitude: null,
  longitude: null,
  hasColdStorage: true,
  fssaiLicenseNo: null,
  status: "ACTIVE",
  operatingHours: [],
  version: "11",
};

function serveLocation(costs: LocationCostRates) {
  server.use(
    http.get(`${API}/admin/fulfillment-locations/${LOCATION_A}`, () => HttpResponse.json(LOCATION)),
    http.get(`${API}/admin/fulfillment-locations/${LOCATION_A}/closures`, () =>
      HttpResponse.json([]),
    ),
    http.get(`${API}/admin/service-areas`, () => HttpResponse.json(page([]))),
    http.get(COSTS_URL, () => HttpResponse.json(costs)),
  );
}

const NOT_CONFIGURED: LocationCostRates = {
  fulfillmentLocationId: LOCATION_A,
  deliveryCostPerDropPaise: null,
  packagingCostPerOrderPaise: null,
  configured: false,
  version: null,
};

const CONFIGURED: LocationCostRates = {
  fulfillmentLocationId: LOCATION_A,
  deliveryCostPerDropPaise: 4500,
  packagingCostPerOrderPaise: 2000,
  configured: true,
  version: "21",
};

function capturePut(reply: () => Response) {
  const seen: { ifMatch: string | null; ifNoneMatch: string | null; body: unknown }[] = [];
  server.use(
    http.put(COSTS_URL, async ({ request }) => {
      seen.push({
        ifMatch: request.headers.get("If-Match"),
        ifNoneMatch: request.headers.get("If-None-Match"),
        body: await request.json(),
      });
      return reply();
    }),
  );
  return seen;
}

describe("Location delivery and packaging cost (D365)", () => {
  it("says not configured and creates the costs in paise with If-None-Match: *", async () => {
    serveLocation(NOT_CONFIGURED);
    const seen = capturePut(() => HttpResponse.json(CONFIGURED));
    const { user } = renderApp({
      path: `/locations/${LOCATION_A}`,
      permissions: ["locations.manage", "delivery.manage"],
    });

    expect(await screen.findByText(/^Not configured\./)).toBeInTheDocument();
    await user.type(screen.getByLabelText(/Rider pay per delivery/), "45");
    await user.type(screen.getByLabelText(/Packaging per order/), "20.50");
    await user.click(screen.getByRole("button", { name: "Save costs" }));

    await waitFor(() => expect(seen).toHaveLength(1));
    expect(seen[0]).toEqual({
      ifMatch: null,
      ifNoneMatch: "*",
      body: { deliveryCostPerDropPaise: 4500, packagingCostPerOrderPaise: 2050 },
    });
  });

  it("updates with If-Match and sends an emptied cost as null (not configured)", async () => {
    serveLocation(CONFIGURED);
    const seen = capturePut(() => HttpResponse.json({ ...NOT_CONFIGURED, version: "22" }));
    const { user } = renderApp({
      path: `/locations/${LOCATION_A}`,
      permissions: ["locations.manage", "delivery.manage"],
    });

    const packaging = await screen.findByLabelText(/Packaging per order/);
    expect(packaging).toHaveValue("20.00");
    expect(screen.queryByText(/^Not configured\./)).not.toBeInTheDocument();
    await user.clear(packaging);
    await user.click(screen.getByRole("button", { name: "Save costs" }));

    await waitFor(() => expect(seen).toHaveLength(1));
    expect(seen[0]).toEqual({
      ifMatch: '"21"',
      ifNoneMatch: null,
      body: { deliveryCostPerDropPaise: 4500, packagingCostPerOrderPaise: null },
    });
  });

  it("refuses a cost above the limit and shows the server's field error", async () => {
    serveLocation(CONFIGURED);
    const seen = capturePut(() =>
      problem(422, "VALIDATION_FAILED", {
        errors: {
          deliveryCostPerDropPaise: [
            {
              code: "VALIDATION_FAILED",
              message: "The deliveryCostPerDropPaise must be between 0 and 100000 paise.",
            },
          ],
        },
      }),
    );
    const { user } = renderApp({
      path: `/locations/${LOCATION_A}`,
      permissions: ["locations.manage", "delivery.manage"],
    });

    const delivery = await screen.findByLabelText(/Rider pay per delivery/);
    await user.clear(delivery);
    await user.type(delivery, "1000.01");
    await user.click(screen.getByRole("button", { name: "Save costs" }));
    expect(
      await screen.findByText(/Enter the rider pay per delivery in rupees/),
    ).toBeInTheDocument();
    expect(seen).toHaveLength(0);

    await user.clear(delivery);
    await user.type(delivery, "60");
    await user.click(screen.getByRole("button", { name: "Save costs" }));
    expect(
      await screen.findByText("The deliveryCostPerDropPaise must be between 0 and 100000 paise."),
    ).toBeInTheDocument();
    expect(delivery).toHaveAttribute("aria-invalid", "true");
  });

  it("is not shown without delivery.manage", async () => {
    serveLocation(CONFIGURED);
    renderApp({ path: `/locations/${LOCATION_A}`, permissions: ["locations.manage"] });

    expect(await screen.findByRole("button", { name: "Save details" })).toBeInTheDocument();
    expect(screen.queryByText("Delivery and packaging cost")).not.toBeInTheDocument();
  });
});
