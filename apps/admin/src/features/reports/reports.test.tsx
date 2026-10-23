import { formatPercentFromBp, formatRupees } from "@fresh/core";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { emptyReport } from "../../test/fixtures";
import { LOCATION_A, renderApp } from "../../test/render";
import { API, server } from "../../test/server";

function dailySales() {
  return {
    ...emptyReport(),
    asOf: "2026-10-24T04:30:00Z",
    fromDate: "2026-10-01",
    toDate: "2026-10-24",
    totals: {
      ordersPlaced: 120,
      ordersDelivered: 110,
      ordersCancelled: 4,
      grossPaise: 98_765_00,
      netRevenuePaise: 87_654_32,
      avgOrderValuePaise: 79_685,
    },
    items: [
      {
        businessDate: "2026-10-24",
        cityId: "c1",
        fulfillmentLocationId: LOCATION_A,
        ordersPlaced: 12,
        ordersDelivered: 11,
        ordersCancelled: 0,
        grossPaise: 9_876_50,
        discountPaise: 0,
        deliveryFeePaise: 0,
        taxPaise: 0,
        netRevenuePaise: 9_876_50,
        refundsPaise: 0,
        postDeliveryRefundsPaise: 0,
        avgOrderValuePaise: 89_786,
      },
    ],
    totalCount: 1,
  };
}

describe("Reports", () => {
  // jsdom has no object URLs; the CSV download saves a Blob through one.
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:report");
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
  });

  it("sends URL filters, the scoped location and default sort; renders server totals", async () => {
    const requests: URL[] = [];
    server.use(
      http.get(`${API}/admin/reports/daily-sales`, ({ request }) => {
        requests.push(new URL(request.url));
        return HttpResponse.json(dailySales());
      }),
    );
    const { router } = renderApp({
      path: "/reports?report=daily-sales&fromDate=2026-10-01&toDate=2026-10-24",
      permissions: ["reports.view"],
    });

    const totals = await screen.findByTestId("report-totals");
    expect(within(totals).getByText(formatRupees(87_654_32))).toBeInTheDocument();
    expect(within(totals).getByText(formatRupees(79_685))).toBeInTheDocument();
    expect(screen.getByText(/Figures as of/)).toBeInTheDocument();

    const first = requests[0]!;
    expect(first.searchParams.get("fromDate")).toBe("2026-10-01");
    expect(first.searchParams.get("toDate")).toBe("2026-10-24");
    expect(first.searchParams.get("fulfillmentLocationId")).toBe(LOCATION_A);
    expect(first.searchParams.get("sort")).toBe("-businessDate");

    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-09-01" } });
    await waitFor(() => expect(requests.at(-1)?.searchParams.get("fromDate")).toBe("2026-09-01"));
    expect(router.state.location.search).toContain("fromDate=2026-09-01");
  });

  it("formats basis points and report-specific filters from the URL", async () => {
    let url: URL | undefined;
    server.use(
      http.get(`${API}/admin/reports/waste`, ({ request }) => {
        url = new URL(request.url);
        return HttpResponse.json({
          ...emptyReport(),
          totals: { movementsCount: 3, wasteCostPaise: 45_000, wasteShareBp: 425 },
        });
      }),
    );
    renderApp({ path: "/reports?report=waste&reasonCode=SPOILED", permissions: ["reports.view"] });
    const totals = await screen.findByTestId("report-totals");
    expect(within(totals).getByText(formatPercentFromBp(425))).toBeInTheDocument();
    expect(url?.searchParams.get("reasonCode")).toBe("SPOILED");
    expect(screen.getByText("Reason: Spoiled")).toBeInTheDocument();
  });

  it("exports CSV with format=csv and without paging", async () => {
    let csvUrl: URL | undefined;
    server.use(
      http.get(`${API}/admin/reports/daily-sales`, ({ request }) => {
        const url = new URL(request.url);
        if (url.searchParams.get("format") === "csv") {
          csvUrl = url;
          return new HttpResponse("business_date\r\n2026-10-24\r\n", {
            headers: {
              "Content-Type": "text/csv",
              "Content-Disposition": 'attachment; filename="daily-sales.csv"',
            },
          });
        }
        return HttpResponse.json(dailySales());
      }),
    );
    const { user } = renderApp({
      path: "/reports?fromDate=2026-10-01",
      permissions: ["reports.view"],
    });
    await screen.findByTestId("report-totals");
    await user.click(screen.getByRole("button", { name: "Export CSV" }));
    await waitFor(() => expect(csvUrl).toBeDefined());
    expect(csvUrl?.searchParams.get("fromDate")).toBe("2026-10-01");
    expect(csvUrl?.searchParams.has("page")).toBe(false);
    expect(await screen.findByText("Report exported")).toBeInTheDocument();
  });
});
