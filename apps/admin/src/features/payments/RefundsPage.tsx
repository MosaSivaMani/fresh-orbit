import { formatDateTime, formatRupees } from "@fresh/core";
import { type ColumnDef, DataTable, EmptyState, FilterBar, StatusBadge } from "@fresh/ui";
import { Link } from "react-router";
import { useRefunds } from "../../api/payments";
import { type Refund } from "../../api/types/payments";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { REFUND_STATUS, statusOptions } from "../../lib/status";
import {
  DateFilter,
  FilterSelect,
  REFUND_METHOD_LABELS,
  REFUND_REASON_LABELS,
  activeFilters,
  options,
} from "./shared";

const FILTERS = ["status", "reasonCode", "fromDate", "toDate"] as const;

const columns: ColumnDef<Refund>[] = [
  {
    id: "requested",
    header: "Requested",
    cell: ({ row }) => (
      <Link
        to={`/refunds/${row.original.id}`}
        className="text-accent-text font-medium hover:underline"
      >
        {formatDateTime(row.original.requestedAt)}
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
    id: "reason",
    header: "Reason",
    cell: ({ row }) => REFUND_REASON_LABELS[row.original.reasonCode] ?? row.original.reasonCode,
  },
  {
    id: "method",
    header: "Method",
    cell: ({ row }) => REFUND_METHOD_LABELS[row.original.method] ?? row.original.method,
  },
  {
    id: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge code={row.original.status} map={REFUND_STATUS} />,
  },
  {
    id: "amount",
    header: "Amount",
    meta: { align: "right" },
    cell: ({ row }) => formatRupees(row.original.amountPaise),
  },
];

export function RefundsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const refunds = useRefunds({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });

  return (
    <>
      <PageHeader
        title="Refunds"
        description={`Refund requests and payouts · ${scope.nameOf(scope.locationId)}`}
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={activeFilters(list, [
            {
              name: "status",
              label: "Status",
              labels: Object.fromEntries(
                statusOptions(REFUND_STATUS).map((o) => [o.value, o.label]),
              ),
            },
            { name: "reasonCode", label: "Reason", labels: REFUND_REASON_LABELS },
            { name: "fromDate", label: "From" },
            { name: "toDate", label: "To" },
          ])}
          onClearAll={() => list.clear(FILTERS)}
        >
          <FilterSelect
            list={list}
            name="status"
            label="Status"
            choices={statusOptions(REFUND_STATUS)}
          />
          <FilterSelect
            list={list}
            name="reasonCode"
            label="Reason"
            choices={options(REFUND_REASON_LABELS)}
          />
          <DateFilter list={list} name="fromDate" label="From date" />
          <DateFilter list={list} name="toDate" label="To date" />
        </FilterBar>
        <DataTable
          caption="Refunds"
          columns={columns}
          data={refunds.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={refunds.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={refunds.isFetching}
          error={tableError(refunds.error)}
          onRetry={() => void refunds.refetch()}
          emptyState={<EmptyState title="No refunds match these filters" />}
        />
      </div>
    </>
  );
}
