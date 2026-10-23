import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  FilterBar,
  FormField,
  Input,
  Select,
} from "@fresh/ui";
import { PackagePlus, Plus } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { useCreateStockItem, useStock } from "../../api/inventory";
import { type StockItem } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { Can, PageHeader, tableError } from "../../components/page";
import { applyFieldErrors } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { parseWholeNumber } from "../../lib/units";
import { qty } from "./maps";
import { InventoryNav, VariantRef } from "./shared";
import { VariantField } from "./VariantField";

const LOW_OPTIONS = [
  { value: "all", label: "All stock items" },
  { value: "true", label: "Low stock only" },
];

export function StockListPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const lowStockOnly = list.get("lowStockOnly") === "true";
  const query = {
    ...list.query(["lowStockOnly", "variantId"]),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  };
  const stock = useStock(query);

  const columns: ColumnDef<StockItem>[] = [
    {
      id: "variant",
      header: "Variant",
      cell: ({ row }) => (
        <Link
          to={`/inventory/stock/${row.original.id}`}
          className="text-accent-text hover:underline"
          aria-label={`Open stock item ${row.original.variantId}`}
        >
          <VariantRef variantId={row.original.variantId} />
        </Link>
      ),
    },
    ...(scope.locationId
      ? []
      : [
          {
            id: "location",
            header: "Location",
            cell: ({ row }) => scope.nameOf(row.original.fulfillmentLocationId),
          } satisfies ColumnDef<StockItem>,
        ]),
    {
      id: "onHand",
      header: "On hand",
      meta: { align: "right" },
      cell: ({ row }) => qty(row.original.onHand),
    },
    {
      id: "reserved",
      header: "Reserved",
      meta: { align: "right" },
      cell: ({ row }) => qty(row.original.reserved),
    },
    {
      id: "available",
      header: "Available",
      meta: { align: "right" },
      cell: ({ row }) => <span className="font-medium">{qty(row.original.available)}</span>,
    },
    {
      id: "threshold",
      header: "Low-stock threshold",
      meta: { align: "right" },
      cell: ({ row }) =>
        qty({ value: row.original.lowStockThreshold, unit: row.original.stockUnit }),
    },
    {
      id: "state",
      header: "State",
      cell: ({ row }) =>
        row.original.isLowStock ? (
          <Badge tone="warning">Low stock</Badge>
        ) : (
          <Badge tone="success">OK</Badge>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Inventory"
        description={`Stock on hand at ${scope.nameOf(scope.locationId)}. Figures come from the stock movements ledger.`}
        actions={
          <>
            <Can perm="inventory.receive">
              <Button asChild leadingIcon={<PackagePlus aria-hidden="true" />}>
                <Link to="/inventory/receive">Receive stock</Link>
              </Button>
            </Can>
            {can("inventory.adjust") && <CreateStockItemButton locationId={scope.locationId} />}
          </>
        }
      />
      <InventoryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={[
            ...(lowStockOnly
              ? [
                  {
                    id: "low",
                    label: "Low stock only",
                    onRemove: () => list.set({ lowStockOnly: null }),
                  },
                ]
              : []),
            ...(list.get("variantId")
              ? [
                  {
                    id: "variant",
                    label: `Variant: …${(list.get("variantId") ?? "").slice(-8)}`,
                    onRemove: () => list.set({ variantId: null }),
                  },
                ]
              : []),
          ]}
          onClearAll={() => list.clear(["lowStockOnly", "variantId"])}
        >
          <div className="w-52">
            <Select
              aria-label="Stock level"
              options={LOW_OPTIONS}
              value={lowStockOnly ? "true" : "all"}
              onValueChange={(v) => list.set({ lowStockOnly: v === "true" ? "true" : null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Stock items"
          columns={columns}
          data={stock.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={stock.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={stock.isFetching}
          error={tableError(stock.error)}
          onRetry={() => void stock.refetch()}
          emptyState={
            <EmptyState
              title={lowStockOnly ? "Nothing is low on stock" : "No stock items yet"}
              description={
                lowStockOnly
                  ? "Every stock item is above its threshold."
                  : "A stock item is created for a variant at a location on its first receipt."
              }
            />
          }
        />
      </div>
    </>
  );
}

function CreateStockItemButton({ locationId }: { locationId: string | null }) {
  const [open, setOpen] = useState(false);
  const [variantId, setVariantId] = useState("");
  const [threshold, setThreshold] = useState("0");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const create = useCreateStockItem();
  const navigate = useNavigate();

  const submit = async () => {
    const next: Record<string, string> = {};
    if (!variantId) next.variantId = "Choose a variant.";
    const t = parseWholeNumber(threshold);
    if (t === null) next.lowStockThreshold = "Enter the threshold as a whole number.";
    setErrors(next);
    if (Object.keys(next).length > 0 || !locationId || t === null) return;
    try {
      const item = await create.mutateAsync({
        fulfillmentLocationId: locationId,
        variantId,
        lowStockThreshold: t,
      });
      notifySaved("Stock item created");
      setOpen(false);
      void navigate(`/inventory/stock/${item.id}`);
    } catch (error) {
      const fieldErrors: Record<string, string> = {};
      const unmatched = applyFieldErrors(
        error,
        (name, e) => {
          fieldErrors[String(name)] = e.message ?? "";
        },
        ["variantId", "lowStockThreshold"],
      );
      setErrors(fieldErrors);
      if (Object.keys(fieldErrors).length === 0 || unmatched.length > 0) reportError(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <Button
        variant="outline"
        leadingIcon={<Plus aria-hidden="true" />}
        onClick={() => setOpen(true)}
      >
        New stock item
      </Button>
      <DialogContent
        title="New stock item"
        description="Track a variant at this location before its first receipt."
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void submit()} loading={create.isPending} disabled={!locationId}>
              Create
            </Button>
          </>
        }
      >
        {!locationId ? (
          <p className="text-text-muted text-sm">
            Choose a fulfillment location in the top bar first.
          </p>
        ) : (
          <div className="flex flex-col gap-4">
            <VariantField value={variantId} onChange={setVariantId} error={errors.variantId} />
            <FormField
              label="Low-stock threshold"
              hint="In the stock unit: grams for weight items, pieces otherwise. 0 turns the alert off."
              error={errors.lowStockThreshold}
              required
            >
              <Input
                inputMode="numeric"
                value={threshold}
                onChange={(e) => setThreshold(e.target.value)}
              />
            </FormField>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
