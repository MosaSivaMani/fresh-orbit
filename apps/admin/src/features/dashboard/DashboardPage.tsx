import { isApiError } from "@fresh/core";
import {
  formatDateTime,
  formatPercentFromBp,
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
  StatusBadge,
} from "@fresh/ui";
import { type ReactNode } from "react";
import { Link } from "react-router";
import { useDashboard, useReport } from "../../api/reports";
import { type DashboardResponse } from "../../api/types/reports";
import { useLocationScope } from "../../app/location-scope";
import { useCan, useSession } from "../../app/services";
import { PageHeader } from "../../components/page";
import { ORDER_STATUS } from "../../lib/status";
import { todayInIndia } from "../../lib/units";

function countOf(data: DashboardResponse | undefined, status: string): number | undefined {
  if (!data) return undefined;
  return data.ordersByStatus.find((s) => s.status === status)?.count ?? 0;
}

function Kpi({
  label,
  value,
  sub,
  to,
  tone,
  loading,
}: {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  to?: string;
  tone?: "danger" | "warning";
  loading: boolean;
}) {
  const body = (
    <Card className="hover:border-border-strong h-full transition-colors">
      <CardContent className="flex flex-col gap-1 p-4">
        <span className="text-text-muted text-sm font-medium">{label}</span>
        {loading ? (
          <Skeleton className="h-8 w-20" />
        ) : (
          <span
            className={`text-2xl leading-8 font-semibold tabular-nums ${
              tone === "danger" ? "text-danger-text" : tone === "warning" ? "text-warning-text" : ""
            }`}
          >
            {value}
          </span>
        )}
        {sub && <span className="text-text-muted text-xs">{sub}</span>}
      </CardContent>
    </Card>
  );
  return to ? (
    <Link
      to={to}
      className="focus-visible:outline-ring rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2"
    >
      {body}
    </Link>
  ) : (
    body
  );
}

export function DashboardPage() {
  const can = useCan();
  const { profile } = useSession();
  const { locationId, nameOf } = useLocationScope();
  const canReports = can("reports.view");
  const today = todayInIndia();
  const scope = locationId ? { fulfillmentLocationId: locationId } : {};
  const dashboard = useDashboard(scope, canReports);
  const sales = useReport("daily-sales", { ...scope, fromDate: today, toDate: today }, canReports);

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
  const salesTotals = sales.data?.totals;
  const ordersPlaced = salesTotals?.ordersPlaced;
  const netRevenue = salesTotals?.netRevenuePaise;
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
              loading={sales.isPending}
              value={typeof ordersPlaced === "number" ? ordersPlaced : "—"}
              sub={
                sales.data?.asOf
                  ? `Report as of ${formatTime(sales.data.asOf)}`
                  : "Report not refreshed yet"
              }
              to="/orders"
            />
            <Kpi
              label="Sales today"
              loading={sales.isPending}
              value={
                typeof netRevenue === "number" ? formatRupees(netRevenue, { paise: "never" }) : "—"
              }
              sub="Net revenue"
              {...(can("reports.view") ? { to: "/reports?report=daily-sales" } : {})}
            />
            <Kpi
              label="Waiting to pack"
              loading={loading}
              value={countOf(data, "CONFIRMED") ?? "—"}
              sub={`Preparing: ${countOf(data, "PREPARING") ?? "—"}`}
              to={orderLink("CONFIRMED")}
            />
            <Kpi
              label="Out for delivery"
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
              tone={data && data.lowStock.length > 0 ? "warning" : undefined}
              to="/inventory?lowStockOnly=true"
            />
            <Kpi
              label="Delivery exceptions"
              loading={loading}
              value={countOf(data, "DELIVERY_FAILED") ?? "—"}
              sub={data ? `Open complaints: ${data.openComplaints}` : undefined}
              tone={(countOf(data, "DELIVERY_FAILED") ?? 0) > 0 ? "danger" : undefined}
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
                              {item.variantName ?? item.sku ?? item.variantId}
                            </Link>
                            {item.sku && (
                              <span className="text-text-muted ml-2 text-xs">{item.sku}</span>
                            )}
                          </td>
                          <td className="py-2 text-right tabular-nums">{item.available}</td>
                          <td className="py-2 text-right tabular-nums">{item.lowStockThreshold}</td>
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
                          className="border-border hover:bg-muted inline-flex items-center gap-2 rounded-md border px-3 py-1.5"
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
