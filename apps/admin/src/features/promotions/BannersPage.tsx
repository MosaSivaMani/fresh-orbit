import { formatDateTime } from "@fresh/core";
import { Badge, Button, type ColumnDef, DataTable, FilterBar, Select } from "@fresh/ui";
import { Plus } from "lucide-react";
import { useMemo } from "react";
import { Link } from "react-router";
import { useCityLookup } from "../../api/locations";
import { useBanners } from "../../api/promotions";
import { type Promotion } from "../../api/types/promotions";
import { PageHeader, tableError } from "../../components/page";
import { ListEmpty } from "../../components/states";
import { useListParams } from "../../lib/list-params";
import { LINK_TYPE_LABELS } from "./shared";

const FILTER_KEYS = ["cityId"] as const;
const ANY = "__any";

/** Home banners (AD-6): the carousel on the customer home, per city or for every city, in sort order. */
export function BannersPage() {
  const list = useListParams({});
  const banners = useBanners(list.query(FILTER_KEYS));
  const cities = useCityLookup();
  const cityName = useMemo(() => {
    const names = new Map((cities.data?.items ?? []).map((c) => [c.id, c.name]));
    return (id: string | null) => (id ? (names.get(id) ?? `City ${id.slice(-6)}`) : "All cities");
  }, [cities.data]);

  const columns = useMemo<ColumnDef<Promotion>[]>(
    () => [
      {
        id: "title",
        header: "Title",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/banners/${row.original.id}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.title}
          </Link>
        ),
      },
      {
        id: "link",
        header: "Opens",
        enableSorting: false,
        cell: ({ row }) => LINK_TYPE_LABELS[row.original.linkType] ?? row.original.linkType,
      },
      {
        id: "city",
        header: "City",
        enableSorting: false,
        cell: ({ row }) => cityName(row.original.cityId),
      },
      {
        id: "when",
        header: "Shown",
        enableSorting: false,
        cell: ({ row }) =>
          `${formatDateTime(row.original.startsAt)} – ${
            row.original.endsAt ? formatDateTime(row.original.endsAt) : "no end"
          }`,
      },
      {
        id: "order",
        header: "Order",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => row.original.sortOrder,
      },
      {
        id: "active",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-wrap gap-1">
            <Badge tone={row.original.isActive ? "success" : "neutral"}>
              {row.original.isActive ? "Active" : "Off"}
            </Badge>
            {row.original.badge && <Badge tone="neutral">{row.original.badge}</Badge>}
          </span>
        ),
      },
    ],
    [cityName],
  );

  const cityId = list.get("cityId");
  return (
    <>
      <PageHeader
        title="Home banners"
        description="The offers carousel on the customer home. Each banner opens a category, a product or a coupon."
        actions={
          <Button asChild leadingIcon={<Plus aria-hidden="true" />}>
            <Link to="/banners/new">New banner</Link>
          </Button>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={
            cityId
              ? [
                  {
                    id: "cityId",
                    label: `City: ${cityName(cityId)}`,
                    onRemove: () => list.set({ cityId: null }),
                  },
                ]
              : []
          }
          onClearAll={() => list.clear([...FILTER_KEYS])}
        >
          <div className="w-48">
            <Select
              aria-label="City"
              value={cityId ?? ANY}
              options={[
                { value: ANY, label: "Any city" },
                ...(cities.data?.items ?? []).map((c) => ({ value: c.id, label: c.name })),
              ]}
              onValueChange={(v) => list.set({ cityId: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Home banners"
          columns={columns}
          data={banners.data?.items ?? []}
          getRowId={(b) => b.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={banners.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={banners.isFetching}
          error={tableError(banners.error)}
          onRetry={() => void banners.refetch()}
          emptyState={
            <ListEmpty
              title="No home banners yet"
              description="Add a banner to show an offer on the customer home."
            />
          }
        />
      </div>
    </>
  );
}
