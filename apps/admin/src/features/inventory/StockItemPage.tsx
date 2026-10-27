import { formatDateTime, formatRupees } from "@fresh/core";
import { Badge, Button, type ColumnDef, DataTable, EmptyState, StatusBadge } from "@fresh/ui";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { useStockItem } from "../../api/inventory";
import { type Batch, type StockItem } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { BATCH_STATUS } from "../../lib/status";
import { qty } from "./maps";
import { VariantRef } from "./shared";
import {
  AdjustDialog,
  CountDialog,
  DiscardBatchDialog,
  ThresholdDialog,
  WasteDialog,
} from "./StockActions";

type Action = "threshold" | "adjust" | "count" | "waste";

export function StockItemPage() {
  const { stockItemId = "" } = useParams();
  const query = useStockItem(stockItemId);
  return (
    <QueryView query={query} what="stock item">
      {(item) => <StockItemView item={item} refetch={() => query.refetch()} />}
    </QueryView>
  );
}

function StockItemView({ item, refetch }: { item: StockItem; refetch: () => unknown }) {
  const can = useCan();
  const scope = useLocationScope();
  const [action, setAction] = useState<Action | null>(null);
  const [discarding, setDiscarding] = useState<Batch | null>(null);
  const canAdjust = can("inventory.adjust");
  const dialog = (a: Action) => ({
    item,
    open: action === a,
    onOpenChange: (open: boolean) => setAction(open ? a : null),
    onConflict: refetch,
  });

  const columns: ColumnDef<Batch>[] = [
    {
      id: "code",
      header: "Batch",
      cell: ({ row }) => <span className="font-medium">{row.original.batchCode}</span>,
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={row.original.status} map={BATCH_STATUS} />,
    },
    {
      id: "received",
      header: "Received",
      cell: ({ row }) => formatDateTime(row.original.receivedAt),
    },
    {
      id: "bestBefore",
      header: "Best before",
      cell: ({ row }) => formatDateTime(row.original.bestBeforeAt),
    },
    {
      id: "remaining",
      header: "Remaining",
      meta: { align: "right" },
      cell: ({ row }) =>
        `${qty(row.original.remainingQuantity)} of ${qty(row.original.receivedQuantity)}`,
    },
    {
      id: "cost",
      header: "Cost per unit",
      meta: { align: "right" },
      cell: ({ row }) => formatRupees(row.original.costPerUnitPaise),
    },
    {
      id: "temperature",
      header: "Receipt °C",
      meta: { align: "right" },
      cell: ({ row }) =>
        row.original.receiptTemperatureC === null ? "—" : `${row.original.receiptTemperatureC} °C`,
    },
    ...(canAdjust
      ? [
          {
            id: "actions",
            header: "Actions",
            cell: ({ row }) => (
              <Button size="sm" variant="ghost" onClick={() => setDiscarding(row.original)}>
                Discard
                <span className="sr-only"> batch {row.original.batchCode}</span>
              </Button>
            ),
          } satisfies ColumnDef<Batch>,
        ]
      : []),
  ];

  return (
    <>
      <PageHeader
        title="Stock item"
        backTo="/inventory"
        backLabel="Inventory"
        meta={
          item.isLowStock ? (
            <Badge tone="warning">Low stock</Badge>
          ) : (
            <Badge tone="success">OK</Badge>
          )
        }
        actions={
          <>
            {can("inventory.receive") && (
              <Button asChild variant="outline">
                <Link to={`/inventory/receive?variantId=${item.variantId}`}>Receive stock</Link>
              </Button>
            )}
            {canAdjust && (
              <>
                <Button variant="outline" onClick={() => setAction("adjust")}>
                  Adjust
                </Button>
                <Button variant="outline" onClick={() => setAction("count")}>
                  Count
                </Button>
                <Button variant="outline" onClick={() => setAction("waste")}>
                  Record waste
                </Button>
              </>
            )}
          </>
        }
      />
      <div className="flex flex-col gap-6">
        <Section
          title="Stock"
          description="Server figures from the stock movements ledger."
          actions={
            canAdjust ? (
              <Button size="sm" variant="ghost" onClick={() => setAction("threshold")}>
                Edit threshold
              </Button>
            ) : undefined
          }
        >
          <DetailList
            columns={3}
            items={[
              { label: "Variant", value: <VariantRef item={item} /> },
              { label: "Location", value: scope.nameOf(item.fulfillmentLocationId) },
              { label: "Stock unit", value: item.stockUnit === "GRAM" ? "Grams" : "Pieces" },
              { label: "On hand", value: qty(item.onHand) },
              { label: "Reserved", value: qty(item.reserved) },
              {
                label: "Available",
                value: <span className="font-semibold">{qty(item.available)}</span>,
              },
              {
                label: "Low-stock threshold",
                value:
                  item.lowStockThreshold === 0
                    ? "Off"
                    : qty({ value: item.lowStockThreshold, unit: item.stockUnit }),
              },
            ]}
          />
          <div>
            <Link
              className="text-accent-text text-sm hover:underline"
              to={`/inventory/movements?stockItemId=${item.id}`}
            >
              View movement history
            </Link>
          </div>
        </Section>
        <Section title="Active batches" description="Earliest best-before is sold first (FEFO).">
          <DataTable
            caption="Active batches"
            columns={columns}
            data={item.batches ?? []}
            getRowId={(b) => b.id}
            // The detail returns every active batch in one response; one page shows them all.
            page={1}
            pageSize={Math.max(item.batches?.length ?? 0, 1)}
            totalCount={item.batches?.length ?? 0}
            onPageChange={() => undefined}
            emptyState={
              <EmptyState title="No active batches" description="Receive stock to add a batch." />
            }
          />
        </Section>
      </div>
      {canAdjust && (
        <>
          <ThresholdDialog {...dialog("threshold")} />
          <AdjustDialog {...dialog("adjust")} />
          <CountDialog {...dialog("count")} />
          <WasteDialog
            item={item}
            open={action === "waste"}
            onOpenChange={(o) => setAction(o ? "waste" : null)}
          />
          {discarding && (
            <DiscardBatchDialog
              batch={discarding}
              fallbackVersion={item.version}
              open
              onOpenChange={(o) => !o && setDiscarding(null)}
              onConflict={refetch}
            />
          )}
        </>
      )}
    </>
  );
}
