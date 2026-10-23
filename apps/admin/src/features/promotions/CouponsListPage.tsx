import { formatDateTime, formatRupees } from "@fresh/core";
import {
  type ActiveFilter,
  Button,
  type ColumnDef,
  DataTable,
  EmptyState,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { Plus, TicketPercent } from "lucide-react";
import { useMemo } from "react";
import { Link } from "react-router";
import { useCoupons } from "../../api/promotions";
import { type Coupon } from "../../api/types/promotions";
import { PageHeader, tableError } from "../../components/page";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { COUPON_STATUS, statusOptions } from "../../lib/status";
import { discountLabel } from "./shared";

const FILTER_KEYS = ["status", "cityId"] as const;
const ANY = "__any";

export function CouponsListPage() {
  const list = useListParams({});
  const search = useDebouncedSearch(list);
  const coupons = useCoupons(list.query(FILTER_KEYS));

  const columns = useMemo<ColumnDef<Coupon>[]>(
    () => [
      {
        id: "code",
        header: "Code",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/promotions/${row.original.id}`}
            className="text-accent-text font-mono font-medium hover:underline"
          >
            {row.original.code}
          </Link>
        ),
      },
      {
        id: "discount",
        header: "Discount",
        enableSorting: false,
        cell: ({ row }) => discountLabel(row.original),
      },
      {
        id: "minOrder",
        header: "Min order",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) =>
          row.original.minOrderPaise > 0 ? formatRupees(row.original.minOrderPaise) : "—",
      },
      {
        id: "used",
        header: "Used",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) =>
          row.original.totalLimit != null
            ? `${row.original.usedCount} / ${row.original.totalLimit}`
            : row.original.usedCount,
      },
      {
        id: "validTo",
        header: "Valid until",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.validTo),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => <StatusBadge code={row.original.status} map={COUPON_STATUS} />,
      },
    ],
    [],
  );

  const status = list.get("status");
  const active: ActiveFilter[] = status
    ? [
        {
          id: "status",
          label: `Status: ${COUPON_STATUS[status]?.label ?? status}`,
          onRemove: () => list.set({ status: null }),
        },
      ]
    : [];

  return (
    <>
      <PageHeader
        title="Coupons"
        description="Discount codes customers apply at checkout. The server checks every rule."
        actions={
          <Button asChild leadingIcon={<Plus aria-hidden="true" />}>
            <Link to="/promotions/new">New coupon</Link>
          </Button>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          search={{ ...search, placeholder: "Code or description", label: "Search coupons" }}
          activeFilters={active}
          onClearAll={() => list.clear([...FILTER_KEYS, "q"])}
        >
          <div className="w-44">
            <Select
              aria-label="Status"
              value={status ?? ANY}
              options={[{ value: ANY, label: "Any status" }, ...statusOptions(COUPON_STATUS)]}
              onValueChange={(v) => list.set({ status: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Coupons"
          columns={columns}
          data={coupons.data?.items ?? []}
          getRowId={(c) => c.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={coupons.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={coupons.isFetching}
          error={tableError(coupons.error)}
          onRetry={() => void coupons.refetch()}
          emptyState={
            <EmptyState
              icon={<TicketPercent aria-hidden="true" />}
              title="No coupons match these filters"
            />
          }
        />
      </div>
    </>
  );
}
