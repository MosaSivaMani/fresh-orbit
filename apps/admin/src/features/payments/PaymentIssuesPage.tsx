import { formatDateTime } from "@fresh/core";
import { Button, type ColumnDef, DataTable, EmptyState, FilterBar, StatusBadge } from "@fresh/ui";
import { useState } from "react";
import { Link } from "react-router";
import { usePaymentIssues, useResolvePaymentIssue } from "../../api/payments";
import { type PaymentIssue } from "../../api/types/payments";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { ISSUE_STATUS, statusOptions } from "../../lib/status";
import {
  FilterSelect,
  ISSUE_KIND_LABELS,
  TextCommandDialog,
  activeFilters,
  options,
} from "./shared";

const FILTERS = ["kind", "status"] as const;

function ResolveButton({ issue, reload }: { issue: PaymentIssue; reload: () => unknown }) {
  const resolve = useResolvePaymentIssue(issue.id);
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button size="sm" variant="outline" onClick={() => setOpen(true)}>
        Resolve
      </Button>
      <TextCommandDialog
        open={open}
        onOpenChange={setOpen}
        title="Resolve payment issue"
        description="Record what was done. The note is kept on the issue."
        field="note"
        label="Note"
        required
        maxLength={500}
        submitLabel="Resolve"
        onConflict={reload}
        onSubmit={async (note) => {
          await resolve.mutateAsync({ note, version: issue.version });
          notifySaved("Issue resolved");
        }}
      />
    </>
  );
}

export function PaymentIssuesPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const issues = usePaymentIssues({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });
  const canManage = can("payments.manage");

  const columns: ColumnDef<PaymentIssue>[] = [
    {
      id: "detected",
      header: "Detected",
      cell: ({ row }) => formatDateTime(row.original.detectedAt),
    },
    {
      id: "kind",
      header: "Kind",
      cell: ({ row }) => ISSUE_KIND_LABELS[row.original.kind] ?? row.original.kind,
    },
    {
      id: "details",
      header: "Details",
      cell: ({ row }) => (
        <span>
          {row.original.details}
          {row.original.note && (
            <span className="text-text-muted block text-xs">Note: {row.original.note}</span>
          )}
        </span>
      ),
    },
    {
      id: "order",
      header: "Order",
      cell: ({ row }) =>
        row.original.orderId ? (
          <Link to={`/orders/${row.original.orderId}`} className="text-accent-text hover:underline">
            View order
          </Link>
        ) : (
          "—"
        ),
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={row.original.status} map={ISSUE_STATUS} />,
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      cell: ({ row }) =>
        canManage && row.original.status === "OPEN" ? (
          <ResolveButton issue={row.original} reload={() => issues.refetch()} />
        ) : null,
    },
  ];

  return (
    <>
      <PageHeader
        title="Payment issues"
        description={`Mismatches and stuck money found by the reconciliation jobs · ${scope.nameOf(scope.locationId)}`}
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={activeFilters(list, [
            { name: "kind", label: "Kind", labels: ISSUE_KIND_LABELS },
            {
              name: "status",
              label: "Status",
              labels: Object.fromEntries(
                statusOptions(ISSUE_STATUS).map((o) => [o.value, o.label]),
              ),
            },
          ])}
          onClearAll={() => list.clear(FILTERS)}
        >
          <FilterSelect list={list} name="kind" label="Kind" choices={options(ISSUE_KIND_LABELS)} />
          <FilterSelect
            list={list}
            name="status"
            label="Status"
            choices={statusOptions(ISSUE_STATUS)}
          />
        </FilterBar>
        <DataTable
          caption="Payment issues"
          columns={columns}
          data={issues.data?.items ?? []}
          getRowId={(i) => i.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={issues.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={issues.isFetching}
          error={tableError(issues.error)}
          onRetry={() => void issues.refetch()}
          emptyState={<EmptyState title="No payment issues match these filters" />}
        />
      </div>
    </>
  );
}
