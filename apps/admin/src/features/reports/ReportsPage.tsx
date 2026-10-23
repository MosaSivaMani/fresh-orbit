import {
  LOCALE,
  formatCalendarDate,
  formatDateTime,
  formatPercentFromBp,
  formatQuantity,
  formatRupees,
  humanizeCode,
} from "@fresh/core";
import {
  type ActiveFilter,
  Button,
  type ColumnDef,
  DataTable,
  EmptyState,
  FilterBar,
  FormField,
  Input,
  Select,
} from "@fresh/ui";
import { BarChart3, Download } from "lucide-react";
import { useMemo } from "react";
import { useCities } from "../../api/locations";
import { useReport, useReportCsv } from "../../api/reports";
import { type ReportQuery, type ReportRow } from "../../api/types/reports";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { DetailList, PageHeader, Section, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import {
  REPORTS,
  type ReportColumn,
  type ReportDefinition,
  reportDefinition,
} from "./report-definitions";

const ANY = "__any";
/** `fulfillmentLocationId=all` in the URL: every location the staff member may see (overrides the switcher). */
const ALL = "all";
const COMMON_KEYS = ["fromDate", "toDate", "cityId", "fulfillmentLocationId"] as const;
const DEFAULT_SORT = "-businessDate";

const counts = new Intl.NumberFormat(LOCALE);
const months = new Intl.DateTimeFormat(LOCALE, {
  timeZone: "UTC",
  month: "short",
  year: "numeric",
});

function formatMonth(isoDate: string): string {
  const [y, m] = isoDate.split("-").map(Number);
  if (!y || !m) return isoDate;
  return months.format(new Date(Date.UTC(y, m - 1, 1)));
}

/** Formats a server value for display; never derives one. */
function formatValue(
  column: ReportColumn,
  value: ReportRow[string] | undefined,
  row: ReportRow | null,
  nameOf: (id: string) => string,
): string {
  if (value === null || value === undefined || value === "") return "—";
  switch (column.kind) {
    case "date":
      return formatCalendarDate(String(value));
    case "month":
      return formatMonth(String(value));
    case "location":
      return nameOf(String(value));
    case "code":
      return humanizeCode(String(value));
    case "money":
      return formatRupees(Number(value));
    case "bp":
      return formatPercentFromBp(Number(value));
    case "count":
      return counts.format(Number(value));
    case "quantity": {
      const unit = row?.stockUnit;
      return typeof unit === "string"
        ? formatQuantity({ value: Number(value), unit })
        : counts.format(Number(value));
    }
    default:
      return String(value);
  }
}

export function ReportsPage() {
  const list = useListParams({ defaultSort: DEFAULT_SORT });
  const report = reportDefinition(list.get("report"));
  const scope = useLocationScope();
  const can = useCan();
  const csv = useReportCsv();
  const cities = useCities({ page: 1, pageSize: 100 }, can("locations.manage"));

  const rawLocation = list.get("fulfillmentLocationId");
  const locationId =
    rawLocation === ALL ? undefined : (rawLocation ?? scope.locationId ?? undefined);

  const query: ReportQuery = {
    fromDate: list.get("fromDate"),
    toDate: list.get("toDate"),
    cityId: list.get("cityId"),
    fulfillmentLocationId: locationId,
    ...Object.fromEntries(report.filters.map((f) => [f.name, list.get(f.name)])),
    ...(report.paged ? { page: list.page, pageSize: list.pageSize, sort: list.sort } : {}),
  };
  const result = useReport(report.key, query);
  const data = result.data;

  const columns = useMemo<ColumnDef<ReportRow>[]>(
    () =>
      report.columns.map((c) => ({
        id: c.name,
        header: c.label,
        enableSorting: report.paged && c.sortable !== false,
        meta: ["count", "money", "bp", "quantity"].includes(c.kind) ? { align: "right" } : {},
        cell: ({ row }) => formatValue(c, row.original[c.name], row.original, scope.nameOf),
      })),
    [report, scope.nameOf],
  );

  const chooseReport = (key: string) =>
    list.set({
      report: key,
      sort: null,
      ...Object.fromEntries(report.filters.map((f) => [f.name, null])),
    });

  const cityName = (id: string) => cities.data?.items.find((c) => c.id === id)?.name ?? "One city";
  const active: ActiveFilter[] = [
    ...(list.get("cityId")
      ? [
          {
            id: "cityId",
            label: `City: ${cityName(list.get("cityId")!)}`,
            onRemove: () => list.set({ cityId: null }),
          },
        ]
      : []),
    ...report.filters
      .filter((f) => list.get(f.name))
      .map((f) => {
        const value = list.get(f.name)!;
        const label =
          f.options?.find((o) => o.value === value)?.label ?? (f.linkOnly ? "selected" : value);
        return {
          id: f.name,
          label: `${f.label}: ${label}`,
          onRemove: () => list.set({ [f.name]: null }),
        };
      }),
  ];

  const rows = data?.items ?? [];

  return (
    <>
      <PageHeader
        title="Reports"
        description={report.description}
        actions={
          <Button
            variant="outline"
            size="sm"
            leadingIcon={<Download aria-hidden="true" />}
            loading={csv.isPending}
            onClick={() =>
              csv.mutate(
                { key: report.key, query },
                {
                  onSuccess: (name) => notifySaved("Report exported", name),
                  onError: (error) => reportError(error, { title: "Could not export the report" }),
                },
              )
            }
          >
            Export CSV
          </Button>
        }
      />
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-end gap-3">
          <FormField label="Report" className="w-60">
            <Select
              value={report.key}
              options={REPORTS.map((r) => ({ value: r.key, label: r.title }))}
              onValueChange={chooseReport}
            />
          </FormField>
          <FormField label="From" className="w-44">
            <Input
              type="date"
              value={list.get("fromDate") ?? data?.fromDate ?? ""}
              onChange={(e) => list.set({ fromDate: e.target.value || null })}
            />
          </FormField>
          <FormField label="To" className="w-44">
            <Input
              type="date"
              value={list.get("toDate") ?? data?.toDate ?? ""}
              onChange={(e) => list.set({ toDate: e.target.value || null })}
            />
          </FormField>
        </div>

        <FilterBar
          activeFilters={active}
          onClearAll={() => list.clear([...COMMON_KEYS, ...report.filters.map((f) => f.name)])}
        >
          <div className="w-56">
            <Select
              aria-label="Location"
              value={rawLocation === ALL || !locationId ? ANY : locationId}
              options={[
                { value: ANY, label: "All my locations" },
                ...scope.locations.map((l) => ({ value: l.id, label: l.name })),
              ]}
              onValueChange={(v) => list.set({ fulfillmentLocationId: v === ANY ? ALL : v })}
            />
          </div>
          {can("locations.manage") && (
            <div className="w-48">
              <Select
                aria-label="City"
                value={list.get("cityId") ?? ANY}
                options={[
                  { value: ANY, label: "All cities" },
                  ...(cities.data?.items ?? []).map((c) => ({ value: c.id, label: c.name })),
                ]}
                onValueChange={(v) => list.set({ cityId: v === ANY ? null : v })}
              />
            </div>
          )}
          <ReportFilters
            report={report}
            get={list.get}
            set={(name, value) => list.set({ [name]: value })}
          />
        </FilterBar>

        <Totals report={report} totals={data?.totals} nameOf={scope.nameOf} />

        <DataTable
          caption={report.title}
          columns={columns}
          data={rows}
          getRowId={(row) => String(rows.indexOf(row))}
          page={report.paged ? list.page : 1}
          pageSize={report.paged ? list.pageSize : Math.max(rows.length, 1)}
          totalCount={data?.totalCount ?? 0}
          onPageChange={list.setPage}
          {...(report.paged
            ? {
                onPageSizeChange: list.setPageSize,
                sorting: list.sorting,
                onSortingChange: list.setSorting,
              }
            : {})}
          isLoading={result.isFetching}
          error={tableError(result.error)}
          onRetry={() => void result.refetch()}
          emptyState={
            <EmptyState
              icon={<BarChart3 aria-hidden="true" />}
              title="No data for these filters"
              description="Try a wider date range or another location."
            />
          }
        />
        <p className="text-text-muted text-xs">
          {data?.asOf
            ? `Figures as of ${formatDateTime(data.asOf)}. Reports refresh periodically; today's orders may not appear yet.`
            : data
              ? "Reports have not been refreshed yet."
              : null}
        </p>
      </div>
    </>
  );
}

function ReportFilters({
  report,
  get,
  set,
}: {
  report: ReportDefinition;
  get: (name: string) => string | undefined;
  set: (name: string, value: string | null) => void;
}) {
  return report.filters
    .filter((f) => !f.linkOnly)
    .map((f) =>
      f.options ? (
        <div key={f.name} className="w-48">
          <Select
            aria-label={f.label}
            value={get(f.name) ?? ANY}
            options={[{ value: ANY, label: `Any ${f.label.toLowerCase()}` }, ...f.options]}
            onValueChange={(v) => set(f.name, v === ANY ? null : v)}
          />
        </div>
      ) : (
        // Keyed by the URL value so "Clear all" and back/forward reset the text.
        <div key={`${f.name}=${get(f.name) ?? ""}`} className="w-48">
          <Input
            aria-label={f.label}
            placeholder={f.label}
            defaultValue={get(f.name) ?? ""}
            onBlur={(e) => set(f.name, e.target.value.trim().toUpperCase() || null)}
            onKeyDown={(e) => {
              if (e.key === "Enter")
                set(f.name, e.currentTarget.value.trim().toUpperCase() || null);
            }}
          />
        </div>
      ),
    );
}

/** Server totals over every matching row (not only this page). */
function Totals({
  report,
  totals,
  nameOf,
}: {
  report: ReportDefinition;
  totals: ReportRow | undefined;
  nameOf: (id: string) => string;
}) {
  if (!totals) return null;
  const items = report.totals
    .filter((c) => c.name in totals)
    .map((c) => ({ label: c.label, value: formatValue(c, totals[c.name], null, nameOf) }));
  if (items.length === 0) return null;
  return (
    <Section title="Totals" description="Across all matching rows, computed by the server.">
      <div data-testid="report-totals">
        <DetailList items={items} columns={3} />
      </div>
    </Section>
  );
}
