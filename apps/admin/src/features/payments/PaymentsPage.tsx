import { formatDateTime, formatRupees } from "@fresh/core";
import { type ColumnDef, DataTable, FilterBar, Input, StatusBadge } from "@fresh/ui";
import { Link } from "react-router";
import { usePayments } from "../../api/payments";
import { type Payment } from "../../api/types/payments";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { PAYMENT_STATUS, statusOptions } from "../../lib/status";
import {
  DateFilter,
  FilterSelect,
  METHOD_LABELS,
  PaymentsTabs,
  activeFilters,
  options,
} from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["status", "method", "provider", "fromDate", "toDate"] as const;

const columns: ColumnDef<Payment>[] = [
  {
    id: "payment",
    header: "Payment",
    cell: ({ row }) => (
      <Link
        to={`/payments/${row.original.id}`}
        className="text-accent-text font-medium hover:underline"
      >
        {row.original.providerPaymentId ??
          row.original.providerOrderId ??
          row.original.id.slice(-8)}
      </Link>
    ),
  },
  {
    id: "order",
    header: "Order",
    cell: ({ row }) => (
      <Link to={`/orders/${row.original.orderId}`} className="text-accent-text hover:underline">
        View order
      </Link>
    ),
  },
  {
    id: "method",
    header: "Method",
    cell: ({ row }) => (
      <span>
        {METHOD_LABELS[row.original.method] ?? row.original.method}
        <span className="text-text-muted block text-xs">{row.original.provider}</span>
      </span>
    ),
  },
  {
    id: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge code={row.original.status} map={PAYMENT_STATUS} />,
  },
  {
    id: "created",
    header: "Created",
    cell: ({ row }) => formatDateTime(row.original.createdAt),
  },
  {
    id: "when",
    header: "Captured / collected",
    cell: ({ row }) => {
      const at = row.original.capturedAt ?? row.original.collectedAt;
      return at ? formatDateTime(at) : "—";
    },
  },
  {
    id: "amount",
    header: "Amount",
    meta: { align: "right" },
    cell: ({ row }) => formatRupees(row.original.amountPaise),
  },
  {
    id: "paid",
    header: "Paid",
    meta: { align: "right" },
    cell: ({ row }) => formatRupees(row.original.summary.paidPaise),
  },
  {
    id: "refunded",
    header: "Refunded",
    meta: { align: "right" },
    cell: ({ row }) => formatRupees(row.original.summary.refundedPaise),
  },
];

export function PaymentsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const query = {
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  };
  const payments = usePayments(query);

  return (
    <>
      <PageHeader
        title="Payments"
        description={`Payment attempts and cash / UPI collections · ${scope.nameOf(scope.locationId)}`}
      />
      <PaymentsTabs current="payments" showWebhooks={scope.allLocations} />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={activeFilters(list, [
            {
              name: "status",
              label: "Status",
              labels: Object.fromEntries(
                statusOptions(PAYMENT_STATUS).map((o) => [o.value, o.label]),
              ),
            },
            { name: "method", label: "Method", labels: METHOD_LABELS },
            { name: "provider", label: "Provider" },
            { name: "fromDate", label: "From" },
            { name: "toDate", label: "To" },
          ])}
          onClearAll={() => list.clear(FILTERS)}
        >
          <FilterSelect
            list={list}
            name="status"
            label="Status"
            choices={statusOptions(PAYMENT_STATUS)}
          />
          <FilterSelect list={list} name="method" label="Method" choices={options(METHOD_LABELS)} />
          <div className="w-40">
            <Input
              aria-label="Provider"
              placeholder="Provider (e.g. cod)"
              defaultValue={list.get("provider") ?? ""}
              onBlur={(e) => list.set({ provider: e.target.value.trim() || null })}
            />
          </div>
          <DateFilter list={list} name="fromDate" label="From date" />
          <DateFilter list={list} name="toDate" label="To date" />
        </FilterBar>
        <DataTable
          caption="Payments"
          columns={columns}
          data={payments.data?.items ?? []}
          getRowId={(p) => p.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={payments.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={payments.isFetching}
          error={tableError(payments.error)}
          onRetry={() => void payments.refetch()}
          emptyState={<ListEmpty title="No payments match these filters" />}
        />
      </div>
    </>
  );
}
