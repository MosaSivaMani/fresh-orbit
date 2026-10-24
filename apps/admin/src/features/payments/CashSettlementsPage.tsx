import { formatCalendarDate, formatRupees, isApiError } from "@fresh/core";
import {
  Button,
  type ColumnDef,
  DataTable,
  FilterBar,
  Input,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { Plus } from "lucide-react";
import { useState } from "react";
import { useCashSettlements, useRiderOptions, useSettlementAction } from "../../api/payments";
import { type CashSettlement, type RiderOption } from "../../api/types/payments";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { SETTLEMENT_STATUS, statusOptions } from "../../lib/status";
import { RecordSettlementDialog } from "./RecordSettlementDialog";
import { DateFilter, FilterSelect, TextCommandDialog, activeFilters } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["riderId", "status", "fromDate", "toDate"] as const;
const ANY = "__any";

function SettlementActions({
  settlement,
  reload,
}: {
  settlement: CashSettlement;
  reload: () => unknown;
}) {
  const action = useSettlementAction(settlement.id);
  const [dialog, setDialog] = useState<"dispute" | "settle" | null>(null);
  if (settlement.status === "SETTLED") return null;
  return (
    <div className="flex justify-end gap-2">
      {settlement.status === "OPEN" && (
        <Button size="sm" variant="outline" onClick={() => setDialog("dispute")}>
          Dispute
        </Button>
      )}
      <Button size="sm" onClick={() => setDialog("settle")}>
        Settle
      </Button>
      <TextCommandDialog
        open={dialog === "dispute"}
        onOpenChange={(open) => setDialog(open ? "dispute" : null)}
        title="Dispute settlement"
        description="Flag this day's cash for follow-up. The reason is kept on the settlement."
        field="reason"
        label="Reason"
        required
        maxLength={500}
        submitLabel="Dispute"
        danger
        onConflict={reload}
        onSubmit={async (reason) => {
          await action.mutateAsync({
            action: "dispute",
            version: settlement.version,
            body: { reason },
          });
          notifySaved("Settlement disputed");
        }}
      />
      <TextCommandDialog
        open={dialog === "settle"}
        onOpenChange={(open) => setDialog(open ? "settle" : null)}
        title="Settle"
        description="Close this day's cash for the rider."
        field="note"
        label="Note"
        required={false}
        maxLength={500}
        submitLabel="Settle"
        onConflict={reload}
        onSubmit={async (note) => {
          await action.mutateAsync({
            action: "settle",
            version: settlement.version,
            body: { note: note || null },
          });
          notifySaved("Settlement settled");
        }}
      />
    </div>
  );
}

export function CashSettlementsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const [recording, setRecording] = useState(false);
  const settlements = useCashSettlements({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });
  const ridersQuery = useRiderOptions(scope.locationId);
  // 403 without delivery.manage: fall back to typing the rider id (API gap noted in docs/design/admin.md).
  const riders: readonly RiderOption[] | null =
    ridersQuery.data?.items ??
    (isApiError(ridersQuery.error) && ridersQuery.error.status === 403 ? null : []);
  const riderName = (id: string) => riders?.find((r) => r.id === id)?.fullName ?? id.slice(-8);

  const columns: ColumnDef<CashSettlement>[] = [
    { id: "date", header: "Date", cell: ({ row }) => formatCalendarDate(row.original.date) },
    { id: "rider", header: "Rider", cell: ({ row }) => riderName(row.original.riderId) },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={row.original.status} map={SETTLEMENT_STATUS} />,
    },
    {
      id: "count",
      header: "Collections",
      meta: { align: "right" },
      cell: ({ row }) => row.original.collectionsCount,
    },
    {
      id: "expected",
      header: "Expected",
      meta: { align: "right" },
      cell: ({ row }) => formatRupees(row.original.expectedPaise),
    },
    {
      id: "received",
      header: "Received",
      meta: { align: "right" },
      cell: ({ row }) => formatRupees(row.original.receivedPaise),
    },
    {
      id: "difference",
      header: "Difference",
      meta: { align: "right" },
      cell: ({ row }) => (
        <span className={row.original.differencePaise === 0 ? "" : "text-danger-text font-medium"}>
          {formatRupees(row.original.differencePaise)}
        </span>
      ),
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      cell: ({ row }) => (
        <SettlementActions settlement={row.original} reload={() => settlements.refetch()} />
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Cash settlements"
        description={`Cash and UPI-on-delivery handed in by riders · ${scope.nameOf(scope.locationId)}`}
        actions={
          <Button leadingIcon={<Plus aria-hidden="true" />} onClick={() => setRecording(true)}>
            Record settlement
          </Button>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={activeFilters(list, [
            {
              name: "riderId",
              label: "Rider",
              labels: Object.fromEntries((riders ?? []).map((r) => [r.id, r.fullName])),
            },
            { name: "status", label: "Status" },
            { name: "fromDate", label: "From" },
            { name: "toDate", label: "To" },
          ])}
          onClearAll={() => list.clear(FILTERS)}
        >
          {riders ? (
            <div className="w-48">
              <Select
                aria-label="Rider"
                options={[
                  { value: ANY, label: "Rider: any" },
                  ...riders.map((r) => ({ value: r.id, label: r.fullName })),
                ]}
                value={list.get("riderId") ?? ANY}
                onValueChange={(v) => list.set({ riderId: v === ANY ? null : v })}
              />
            </div>
          ) : (
            <div className="w-48">
              <Input
                aria-label="Rider id"
                placeholder="Rider id"
                defaultValue={list.get("riderId") ?? ""}
                onBlur={(e) => list.set({ riderId: e.target.value.trim() || null })}
              />
            </div>
          )}
          <FilterSelect
            list={list}
            name="status"
            label="Status"
            choices={statusOptions(SETTLEMENT_STATUS)}
          />
          <DateFilter list={list} name="fromDate" label="From date" />
          <DateFilter list={list} name="toDate" label="To date" />
        </FilterBar>
        <DataTable
          caption="Cash settlements"
          columns={columns}
          data={settlements.data?.items ?? []}
          getRowId={(s) => s.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={settlements.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={settlements.isFetching}
          error={tableError(settlements.error)}
          onRetry={() => void settlements.refetch()}
          emptyState={<ListEmpty title="No settlements match these filters" />}
        />
      </div>
      <RecordSettlementDialog open={recording} onOpenChange={setRecording} riders={riders} />
    </>
  );
}
