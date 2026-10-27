import { formatDateTime, humanizeCode } from "@fresh/core";
import {
  type ActiveFilter,
  type ColumnDef,
  DataTable,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { useMemo } from "react";
import { Link } from "react-router";
import { useComplaints } from "../../api/orders";
import { type ComplaintSummary, complaintItemLabel } from "../../api/types/orders";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { COMPLAINT_STATUS, statusOptions } from "../../lib/status";
import { ListEmpty } from "../../components/states";

const FILTER_KEYS = ["status", "kind"] as const;
const KINDS = ["MISSING", "DAMAGED", "QUALITY", "WRONG_ITEM", "LATE"];
const ANY = "__any";

export function ComplaintsListPage() {
  const list = useListParams({});
  const { locationId, nameOf } = useLocationScope();
  const query = {
    ...list.query(FILTER_KEYS),
    ...(locationId ? { fulfillmentLocationId: locationId } : {}),
  };
  const complaints = useComplaints(query);

  const columns = useMemo<ColumnDef<ComplaintSummary>[]>(
    () => [
      {
        id: "order",
        header: "Order",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/complaints/${row.original.id}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.orderNumber}
          </Link>
        ),
      },
      {
        id: "kind",
        header: "Kind",
        enableSorting: false,
        cell: ({ row }) => humanizeCode(row.original.kind),
      },
      {
        id: "item",
        header: "Item",
        enableSorting: false,
        cell: ({ row }) => complaintItemLabel(row.original),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => <StatusBadge code={row.original.status} map={COMPLAINT_STATUS} />,
      },
      {
        id: "raisedAt",
        header: "Raised",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.raisedAt),
      },
      ...(locationId
        ? []
        : [
            {
              id: "location",
              header: "Location",
              enableSorting: false,
              cell: ({ row }: { row: { original: ComplaintSummary } }) =>
                nameOf(row.original.fulfillmentLocationId),
            } satisfies ColumnDef<ComplaintSummary>,
          ]),
    ],
    [locationId, nameOf],
  );

  const status = list.get("status");
  const kind = list.get("kind");
  const active: ActiveFilter[] = [
    ...(status
      ? [
          {
            id: "status",
            label: `Status: ${COMPLAINT_STATUS[status]?.label ?? status}`,
            onRemove: () => list.set({ status: null }),
          },
        ]
      : []),
    ...(kind
      ? [
          {
            id: "kind",
            label: `Kind: ${humanizeCode(kind)}`,
            onRemove: () => list.set({ kind: null }),
          },
        ]
      : []),
  ];

  return (
    <>
      <PageHeader
        title="Complaints"
        description="Missing, damaged, quality, wrong item and late delivery reports."
      />
      <div className="flex flex-col gap-4">
        <FilterBar activeFilters={active} onClearAll={() => list.clear([...FILTER_KEYS])}>
          <div className="w-44">
            <Select
              aria-label="Status"
              value={status ?? ANY}
              options={[{ value: ANY, label: "Any status" }, ...statusOptions(COMPLAINT_STATUS)]}
              onValueChange={(v) => list.set({ status: v === ANY ? null : v })}
            />
          </div>
          <div className="w-44">
            <Select
              aria-label="Kind"
              value={kind ?? ANY}
              options={[
                { value: ANY, label: "Any kind" },
                ...KINDS.map((k) => ({ value: k, label: humanizeCode(k) })),
              ]}
              onValueChange={(v) => list.set({ kind: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Complaints"
          columns={columns}
          data={complaints.data?.items ?? []}
          getRowId={(c) => c.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={complaints.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={complaints.isFetching}
          error={tableError(complaints.error)}
          onRetry={() => void complaints.refetch()}
          emptyState={<ListEmpty title="No complaints match these filters" />}
        />
      </div>
    </>
  );
}
