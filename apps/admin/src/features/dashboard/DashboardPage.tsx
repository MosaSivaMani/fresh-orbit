import { isApiError } from "@fresh/core";
import {
  formatDateTime,
  formatPercentFromBp,
  formatQuantity,
  formatRupees,
  formatTime,
  humanizeCode,
} from "@fresh/core";
import {
  Badge,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  EmptyState,
  ErrorState,
  Skeleton,
  StatTile,
  type StatTileProps,
  StatusBadge,
} from "@fresh/ui";
import { Link } from "react-router";
import { useDashboard } from "../../api/reports";
import { type DashboardLowStock, type DashboardResponse } from "../../api/types/reports";
import { useLocationScope } from "../../app/location-scope";
import { useCan, useSession } from "../../app/services";
import { PageHeader } from "../../components/page";
import { ORDER_STATUS } from "../../lib/status";

function countOf(data: DashboardResponse | undefined, status: string): number | undefined {
  if (!data) return undefined;
  return data.ordersByStatus.find((s) => s.status === status)?.count ?? 0;
}

/** A dashboard figure: the shared `StatTile`, as a link to the list behind it. */
function Kpi({ to, ...props }: StatTileProps & { to?: string }) {
  const tile = <StatTile {...props} />;
  return to ? (
    <Link
      to={to}
      className="focus-visible:outline-ring rounded-3xl focus-visible:outline-2 focus-visible:outline-offset-2"
    >
      {tile}
    </Link>
  ) : (
    tile
  );
}

/** "Seer fish · Steaks" as the server names the stock item, else its SKU or variant id. */
function lowStockName(item: DashboardLowStock): string {
  const named = [item.productName, item.variantName].filter(Boolean).join(" · ");
  return named || (item.sku ?? item.variantId);
}

/** A low-stock figure in the server's stock unit (G4); the bare number when the unit is not known. */
function stockAmount(value: number, unit: string | null): string {
  return unit === "GRAM" || unit === "PIECE" ? formatQuantity({ value, unit }) : String(value);
}

export function DashboardPage() {
  const can = useCan();
  const { profile } = useSession();
  const { locationId, nameOf } = useLocationScope();
  const canReports = can("reports.view");
  const scope = locationId ? { fulfillmentLocationId: locationId } : {};
  const dashboard = useDashboard(scope, canReports);

  if (!canReports) {
    return (
      <>
        <PageHeader title={`Welcome${profile ? `, ${profile.fullName}` : ""}`} />
        <EmptyState
          title="Pick a section from the menu"
          description="The dashboard figures need the reports permission. Your menu shows the sections you can use."
        />
      </>
    );
  }

  const data = dashboard.data;
  const loading = dashboard.isPending;
  // Today's figures, computed live by the server per location date (G4).
  const sales = data?.sales;
  const orderLink = (status: string) =>
    `/orders?status=${status}${locationId ? `&fulfillmentLocationId=${locationId}` : ""}`;

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={
          data ? `${nameOf(locationId)} · updated ${formatDateTime(data.asOf)}` : nameOf(locationId)
        }
      />

      {dashboard.error ? (
        <ErrorState
          title="Could not load the dashboard"
          traceId={isApiError(dashboard.error) ? dashboard.error.traceId : undefined}
          onRetry={() => void dashboard.refetch()}
        />
      ) : (
        <div className="flex flex-col gap-6">
          <section
            aria-label="Today at a glance"
            className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6"
          >
            <Kpi
              label="Orders today"
              tone="teal"
              loading={loading}
              value={sales ? sales.ordersPlaced : "—"}
              sub={sales ? `Cancelled: ${sales.ordersCancelled}` : undefined}
              to="/orders"
            />
            <Kpi
              label="Sales today"
              tone="coral"
              loading={loading}
              value={sales ? formatRupees(sales.orderValuePaise, { paise: "never" }) : "—"}
              sub={
                sales
                  ? `Delivered: ${formatRupees(sales.deliveredRevenuePaise, { paise: "never" })}`
                  : undefined
              }
              to="/reports?report=daily-sales"
            />
            <Kpi
              label="Waiting to pack"
              tone="mango"
              loading={loading}
              value={countOf(data, "CONFIRMED") ?? "—"}
              sub={`Preparing: ${countOf(data, "PREPARING") ?? "—"}`}
              to={orderLink("CONFIRMED")}
            />
            <Kpi
              label="Out for delivery"
              tone="ice"
              loading={loading}
              value={countOf(data, "OUT_FOR_DELIVERY") ?? "—"}
              sub={`Packed: ${countOf(data, "PACKED") ?? "—"}`}
              to={orderLink("OUT_FOR_DELIVERY")}
            />
            <Kpi
              label="Low stock"
              loading={loading}
              value={data ? data.lowStock.length : "—"}
              sub="Items at or below threshold"
              emphasis={data && data.lowStock.length > 0 ? "warning" : undefined}
              to="/inventory?lowStockOnly=true"
            />
            <Kpi
              label="Delivery exceptions"
              loading={loading}
              value={countOf(data, "DELIVERY_FAILED") ?? "—"}
              sub={data ? `Open complaints: ${data.openComplaints}` : undefined}
              emphasis={(countOf(data, "DELIVERY_FAILED") ?? 0) > 0 ? "danger" : undefined}
              to={orderLink("DELIVERY_FAILED")}
            />
          </section>

          <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Today's slots</CardTitle>
              </CardHeader>
              <CardContent>
                {loading ? (
                  <Skeleton className="h-32 w-full" />
                ) : !data || data.slots.length === 0 ? (
                  <p className="text-text-muted text-sm">No delivery slots today.</p>
                ) : (
                  <table className="w-full text-sm">
                    <caption className="sr-only">Today's delivery slots</caption>
                    <thead className="text-text-muted text-left text-xs uppercase">
                      <tr className="border-border border-b">
                        <th scope="col" className="py-2">
                          Slot
                        </th>
                        {!locationId && (
                          <th scope="col" className="py-2">
                            Location
                          </th>
                        )}
                        <th scope="col" className="py-2 text-right">
                          Booked
                        </th>
                        <th scope="col" className="py-2 text-right">
                          Fill
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.slots.map((slot) => (
                        <tr key={slot.slotId} className="border-border border-b last:border-0">
                          <td className="py-2">
                            {formatTime(slot.startAt)} – {formatTime(slot.endAt)}
                          </td>
                          {!locationId && (
                            <td className="py-2">{nameOf(slot.fulfillmentLocationId)}</td>
                          )}
                          <td className="py-2 text-right tabular-nums">
                            {slot.bookedCount} / {slot.capacity}
                          </td>
                          <td className="py-2 text-right tabular-nums">
                            {slot.fillBp == null ? "—" : formatPercentFromBp(slot.fillBp)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Low stock</CardTitle>
              </CardHeader>
              <CardContent>
                {loading ? (
                  <Skeleton className="h-32 w-full" />
                ) : !data || data.lowStock.length === 0 ? (
                  <p className="text-text-muted text-sm">Nothing is below its threshold.</p>
                ) : (
                  <table className="w-full text-sm">
                    <caption className="sr-only">Stock items at or below their threshold</caption>
                    <thead className="text-text-muted text-left text-xs uppercase">
                      <tr className="border-border border-b">
                        <th scope="col" className="py-2">
                          Item
                        </th>
                        <th scope="col" className="py-2 text-right">
                          Available
                        </th>
                        <th scope="col" className="py-2 text-right">
                          Threshold
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.lowStock.map((item) => (
                        <tr key={item.stockItemId} className="border-border border-b last:border-0">
                          <td className="py-2">
                            <Link
                              to={`/inventory/stock/${item.stockItemId}`}
                              className="text-accent-text hover:underline"
                            >
                              {lowStockName(item)}
                            </Link>
                            {item.sku && (
                              <span className="text-text-muted ml-2 text-xs">{item.sku}</span>
                            )}
                          </td>
                          <td className="py-2 text-right tabular-nums">
                            {stockAmount(item.available, item.stockUnit)}
                          </td>
                          <td className="py-2 text-right tabular-nums">
                            {stockAmount(item.lowStockThreshold, item.stockUnit)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Orders by status</CardTitle>
              </CardHeader>
              <CardContent>
                {loading ? (
                  <Skeleton className="h-24 w-full" />
                ) : !data || data.ordersByStatus.length === 0 ? (
                  <p className="text-text-muted text-sm">No orders today.</p>
                ) : (
                  <ul className="flex flex-wrap gap-2">
                    {data.ordersByStatus.map((s) => (
                      <li key={s.status}>
                        <Link
                          to={orderLink(s.status)}
                          className="border-border hover:bg-muted inline-flex items-center gap-2 rounded-full border px-3 py-1.5"
                        >
                          <StatusBadge code={s.status} map={ORDER_STATUS} />
                          <span className="font-semibold tabular-nums">{s.count}</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Needs attention</CardTitle>
              </CardHeader>
              <CardContent>
                {loading || !data ? (
                  <Skeleton className="h-24 w-full" />
                ) : (
                  <ul className="flex flex-col gap-2 text-sm">
                    <Attention
                      label="Open complaints"
                      count={data.openComplaints}
                      to="/complaints?status=OPEN"
                    />
                    <Attention
                      label="Refunds waiting for approval"
                      count={data.refundsRequested}
                      extra={
                        data.refundsRequested > 0
                          ? formatRupees(data.refundsRequestedPaise)
                          : undefined
                      }
                      to="/refunds?status=REQUESTED"
                    />
                    <Attention
                      label="Open payment issues"
                      count={data.openPaymentIssues}
                      to="/payment-issues?status=OPEN"
                    />
                    <Attention
                      label={humanizeCode("DELIVERY_FAILED")}
                      count={countOf(data, "DELIVERY_FAILED") ?? 0}
                      to={orderLink("DELIVERY_FAILED")}
                    />
                  </ul>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </>
  );
}

function Attention({
  label,
  count,
  extra,
  to,
}: {
  label: string;
  count: number;
  extra?: string;
  to: string;
}) {
  return (
    <li className="flex items-center justify-between gap-3">
      <Link to={to} className="text-accent-text hover:underline">
        {label}
      </Link>
      <span className="flex items-center gap-2">
        {extra && <span className="text-text-muted tabular-nums">{extra}</span>}
        <Badge tone={count > 0 ? "warning" : "neutral"}>{count}</Badge>
      </span>
    </li>
  );
}
