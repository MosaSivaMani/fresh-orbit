import { humanizeCode } from "@fresh/core";
import {
  type ActiveFilter,
  Badge,
  type ColumnDef,
  DataTable,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { useMemo } from "react";
import { Link } from "react-router";
import { useCustomers } from "../../api/customers";
import { useCityLookup } from "../../api/locations";
import { type CustomerListItem } from "../../api/types/customers";
import { PageHeader, tableError } from "../../components/page";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { CUSTOMER_STATUS } from "../../lib/status";
import { ListEmpty } from "../../components/states";

const FILTER_KEYS = ["status", "cityId"] as const;
const STATUSES = [
  { value: "ACTIVE", label: "Active" },
  { value: "DELETED", label: "Deleted" },
];
const ANY = "__any";

export function CustomersListPage() {
  const list = useListParams();
  const search = useDebouncedSearch(list);
  const customers = useCustomers(list.query(FILTER_KEYS));
  const status = list.get("status");
  const cityId = list.get("cityId");
  const cities = useCityLookup();
  const cityName = cities.data?.items.find((c) => c.id === cityId)?.name ?? "One city";

  const columns = useMemo<ColumnDef<CustomerListItem>[]>(
    () => [
      {
        id: "fullName",
        header: "Name",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/customers/${row.original.customerId}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.fullName ?? "Unnamed customer"}
          </Link>
        ),
      },
      {
        id: "phone",
        header: "Phone",
        enableSorting: false,
        cell: ({ row }) => row.original.phone ?? "—",
      },
      {
        id: "email",
        header: "Email",
        enableSorting: false,
        cell: ({ row }) => row.original.email ?? "—",
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-wrap gap-1">
            <StatusBadge code={row.original.status} map={CUSTOMER_STATUS} />
            {row.original.isBlocked && <Badge tone="danger">Blocked</Badge>}
          </span>
        ),
      },
    ],
    [],
  );

  const active: ActiveFilter[] = [
    ...(status
      ? [
          {
            id: "status",
            label: `Status: ${STATUSES.find((s) => s.value === status)?.label ?? humanizeCode(status)}`,
            onRemove: () => list.set({ status: null }),
          },
        ]
      : []),
    ...(cityId
      ? [{ id: "cityId", label: `City: ${cityName}`, onRemove: () => list.set({ cityId: null }) }]
      : []),
  ];

  return (
    <>
      <PageHeader title="Customers" description="Search by name, phone or email." />
      <div className="flex flex-col gap-4">
        <FilterBar
          search={{ ...search, placeholder: "Name, phone or email", label: "Search customers" }}
          activeFilters={active}
          onClearAll={() => list.clear([...FILTER_KEYS, "q"])}
        >
          <div className="w-44">
            <Select
              aria-label="Status"
              value={status ?? ANY}
              options={[{ value: ANY, label: "Any status" }, ...STATUSES]}
              onValueChange={(v) => list.set({ status: v === ANY ? null : v })}
            />
          </div>
          <div className="w-48">
            <Select
              aria-label="City"
              value={cityId ?? ANY}
              options={[
                { value: ANY, label: "All cities" },
                ...(cities.data?.items ?? []).map((c) => ({ value: c.id, label: c.name })),
              ]}
              onValueChange={(v) => list.set({ cityId: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>

        <DataTable
          caption="Customers"
          columns={columns}
          data={customers.data?.items ?? []}
          getRowId={(c) => c.customerId}
          getRowLabel={(c) => c.fullName ?? c.phone ?? c.customerId}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={customers.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={customers.isFetching}
          error={tableError(customers.error)}
          onRetry={() => void customers.refetch()}
          emptyState={
            <ListEmpty
              title="No customers match"
              description={
                active.length > 0 || list.q ? "Clear the filters to see all customers." : undefined
              }
            />
          }
        />
      </div>
    </>
  );
}
