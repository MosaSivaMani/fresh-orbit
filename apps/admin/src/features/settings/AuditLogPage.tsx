import { formatDateTime, humanizeCode } from "@fresh/core";
import { type ActiveFilter, type ColumnDef, DataTable, FilterBar, Input } from "@fresh/ui";
import { useMemo } from "react";
import { useAuditLog } from "../../api/audit";
import { type AuditEntry } from "../../api/types/audit";
import { PageHeader, tableError } from "../../components/page";
import { useListParams } from "../../lib/list-params";
import { ListEmpty } from "../../components/states";

const TEXT_FILTERS = [
  { key: "entityType", label: "Entity type", upper: true },
  { key: "entityId", label: "Entity id", upper: false },
  { key: "action", label: "Action", upper: true },
  { key: "actorId", label: "Actor id", upper: false },
] as const;
const DATE_FILTERS = [
  { key: "fromDate", label: "From" },
  { key: "toDate", label: "To" },
] as const;
const FILTER_KEYS = [...TEXT_FILTERS.map((f) => f.key), ...DATE_FILTERS.map((f) => f.key)];

function Snapshot({ label, value }: { label: string; value: unknown }) {
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-1">
      <span className="text-text-muted text-xs font-medium uppercase">{label}</span>
      <pre className="bg-muted max-h-72 overflow-auto rounded-md p-2 text-xs whitespace-pre-wrap">
        {value == null ? "—" : JSON.stringify(value, null, 2)}
      </pre>
    </div>
  );
}

export function AuditLogPage() {
  const list = useListParams();
  const audit = useAuditLog(list.query(FILTER_KEYS));

  const columns = useMemo<ColumnDef<AuditEntry>[]>(
    () => [
      {
        id: "occurredAt",
        header: "When",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.occurredAt),
      },
      {
        id: "actor",
        header: "Who",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span>{row.original.actorName ?? humanizeCode(row.original.actorType)}</span>
            {row.original.ip && <span className="text-text-muted text-xs">{row.original.ip}</span>}
          </span>
        ),
      },
      {
        id: "action",
        header: "Action",
        enableSorting: false,
        cell: ({ row }) => humanizeCode(row.original.action),
      },
      {
        id: "entity",
        header: "Record",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span>{humanizeCode(row.original.entityType)}</span>
            <span className="text-text-muted font-mono text-xs">{row.original.entityId}</span>
          </span>
        ),
      },
      {
        id: "changes",
        header: "Changes",
        enableSorting: false,
        cell: ({ row }) => (
          <details>
            <summary className="text-accent-text cursor-pointer text-sm">Before and after</summary>
            <div className="mt-2 flex w-[min(48rem,80vw)] flex-col gap-2 md:flex-row">
              <Snapshot label="Before" value={row.original.before} />
              <Snapshot label="After" value={row.original.after} />
            </div>
          </details>
        ),
      },
    ],
    [],
  );

  const active: ActiveFilter[] = FILTER_KEYS.filter((k) => list.get(k)).map((k) => ({
    id: k,
    label: `${[...TEXT_FILTERS, ...DATE_FILTERS].find((f) => f.key === k)?.label}: ${list.get(k)}`,
    onRemove: () => list.set({ [k]: null }),
  }));

  return (
    <>
      <PageHeader
        title="Audit log"
        description="Every change staff and the system made, newest first."
      />
      <div className="flex flex-col gap-4">
        <FilterBar activeFilters={active} onClearAll={() => list.clear(FILTER_KEYS)}>
          {TEXT_FILTERS.map((f) => (
            <div key={`${f.key}=${list.get(f.key) ?? ""}`} className="w-44">
              <Input
                aria-label={f.label}
                placeholder={f.label}
                defaultValue={list.get(f.key) ?? ""}
                onBlur={(e) => commit(f.key, e.target.value, f.upper)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") commit(f.key, e.currentTarget.value, f.upper);
                }}
              />
            </div>
          ))}
          {DATE_FILTERS.map((f) => (
            <div key={f.key} className="w-44">
              <Input
                type="date"
                aria-label={f.label}
                value={list.get(f.key) ?? ""}
                onChange={(e) => list.set({ [f.key]: e.target.value || null })}
              />
            </div>
          ))}
        </FilterBar>
        <DataTable
          caption="Audit log"
          columns={columns}
          data={audit.data?.items ?? []}
          getRowId={(e) => String(e.id)}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={audit.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={audit.isFetching}
          error={tableError(audit.error)}
          onRetry={() => void audit.refetch()}
          emptyState={
            <ListEmpty
              title="No entries match"
              description={active.length > 0 ? "Clear the filters to see all entries." : undefined}
            />
          }
        />
      </div>
    </>
  );

  function commit(key: string, raw: string, upper: boolean) {
    const value = raw.trim();
    const next = upper ? value.toUpperCase() : value;
    if (next !== (list.get(key) ?? "")) list.set({ [key]: next || null });
  }
}
