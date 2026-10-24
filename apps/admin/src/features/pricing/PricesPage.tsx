import { formatDateTime, formatRupees } from "@fresh/core";
import { Badge, Button, type ColumnDef, DataTable, FilterBar, Switch } from "@fresh/ui";
import { Percent, Upload } from "lucide-react";
import { useMemo, useState } from "react";
import { Link } from "react-router";
import { usePrices } from "../../api/pricing";
import { type PriceListItem } from "../../api/types/pricing";
import { useLocationScope } from "../../app/location-scope";
import { LocationRequired, PageHeader, tableError } from "../../components/page";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { SetPriceDialog } from "./SetPriceDialog";
import { PRICE_UNIT } from "./shared";
import { ListEmpty } from "../../components/states";

export function PricesPage() {
  const scope = useLocationScope();
  return (
    <>
      <PageHeader
        title="Prices"
        description={
          scope.locationId
            ? `Selling prices at ${scope.nameOf(scope.locationId)}. A variant without a price is not for sale there.`
            : "Selling prices per fulfillment location."
        }
        actions={
          <>
            <Button asChild variant="outline" leadingIcon={<Percent aria-hidden="true" />}>
              <Link to="/pricing/tax-rules">Tax rules</Link>
            </Button>
            <Button asChild variant="outline" leadingIcon={<Upload aria-hidden="true" />}>
              <Link to="/pricing/bulk">Bulk upload</Link>
            </Button>
          </>
        }
      />
      {scope.locationId ? (
        <PriceList locationId={scope.locationId} />
      ) : (
        <LocationRequired action="see and set prices" />
      )}
    </>
  );
}

function PriceList({ locationId }: { locationId: string }) {
  const list = useListParams();
  const search = useDebouncedSearch(list);
  const missingOnly = list.get("missingOnly") === "true";
  const prices = usePrices({
    fulfillmentLocationId: locationId,
    page: list.page,
    pageSize: list.pageSize,
    ...(list.q ? { q: list.q } : {}),
    ...(missingOnly ? { missingOnly: true } : {}),
  });
  const [editing, setEditing] = useState<PriceListItem | null>(null);

  const columns = useMemo<ColumnDef<PriceListItem>[]>(
    () => [
      {
        id: "variant",
        header: "Product / variant",
        cell: ({ row }) => (
          <span className="flex flex-col">
            <Link
              to={`/pricing/variants/${row.original.variantId}`}
              className="text-accent-text font-medium hover:underline"
            >
              {row.original.productName} · {row.original.variantName}
            </Link>
            <span className="text-text-muted font-mono text-xs">{row.original.sku}</span>
          </span>
        ),
      },
      {
        id: "unit",
        header: "Unit",
        cell: ({ row }) => PRICE_UNIT[row.original.priceUnit] ?? row.original.priceUnit,
      },
      {
        id: "current",
        header: "Current price",
        meta: { align: "right" },
        cell: ({ row }) => {
          const c = row.original.current;
          if (!c) return <Badge tone="warning">No price</Badge>;
          return (
            <span className="flex flex-col items-end">
              <span className="font-medium tabular-nums">{formatRupees(c.unitPricePaise)}</span>
              {c.compareAtPaise != null && (
                <span className="text-text-muted text-xs line-through">
                  {formatRupees(c.compareAtPaise)}
                </span>
              )}
            </span>
          );
        },
      },
      {
        id: "next",
        header: "Scheduled",
        cell: ({ row }) => {
          const n = row.original.next;
          return n ? (
            <span className="text-sm">
              {formatRupees(n.unitPricePaise)} from {formatDateTime(n.validFrom)}
            </span>
          ) : (
            <span className="text-text-muted">—</span>
          );
        },
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        meta: { align: "right" },
        cell: ({ row }) => (
          <Button
            variant="outline"
            size="sm"
            aria-label={`Set price for ${row.original.sku}`}
            onClick={() => setEditing(row.original)}
          >
            Set price
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        search={{
          ...search,
          placeholder: "Search product, variant or SKU…",
          label: "Search prices",
        }}
        activeFilters={
          missingOnly
            ? [
                {
                  id: "missingOnly",
                  label: "Without a price",
                  onRemove: () => list.set({ missingOnly: null }),
                },
              ]
            : []
        }
        onClearAll={() => list.clear(["q", "missingOnly"])}
      >
        <Switch
          label="Only without a price"
          checked={missingOnly}
          onCheckedChange={(on) => list.set({ missingOnly: on ? "true" : null })}
        />
      </FilterBar>
      <DataTable
        caption="Prices"
        columns={columns}
        data={prices.data?.items ?? []}
        getRowId={(r) => r.variantId}
        page={prices.data?.page ?? list.page}
        pageSize={prices.data?.pageSize ?? list.pageSize}
        totalCount={prices.data?.totalCount ?? 0}
        onPageChange={list.setPage}
        onPageSizeChange={list.setPageSize}
        isLoading={prices.isFetching}
        error={tableError(prices.error)}
        onRetry={() => void prices.refetch()}
        emptyState={
          <ListEmpty
            title={
              list.q || missingOnly ? "No variants match these filters" : "No variants to price"
            }
            description="Active variants of products in the catalogue appear here."
          />
        }
      />
      {editing && (
        <SetPriceDialog
          locationId={locationId}
          variantId={editing.variantId}
          title={`${editing.productName} · ${editing.variantName}`}
          priceUnit={editing.priceUnit}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}
