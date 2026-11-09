import {
  type QuantityUnit,
  formatCalendarDate,
  formatPercentFromBp,
  formatQuantity,
  formatWeight,
} from "@fresh/core";
import { Badge, Button, FormField, Input } from "@fresh/ui";
import { Download, Printer } from "lucide-react";
import { useState } from "react";
import { useBuyingList, useBuyingListCsv } from "../../api/buying";
import {
  type BuyingListItem,
  type BuyingListPreparation,
  type BuyingListQuery,
  type BuyingListResponse,
} from "../../api/types/buying";
import { useLocationScope } from "../../app/location-scope";
import { LocationRequired, PageHeader, QueryView } from "../../components/page";
import { ListEmpty } from "../../components/states";
import { reportError } from "../../lib/feedback";
import { parsePercentToBp } from "../../lib/units";
import { InventoryNav } from "./shared";

/**
 * The morning buying list (REP-R14, D369): the confirmed orders of one delivery date summed per variant and
 * preparation, turned into raw fish to buy with each order's cleaning yield, set against the stock on hand. The safety
 * margin is typed here and sent with the query, never stored. Every number is the server's; "Print" prints only the
 * list (`fo-print-area`).
 */
export function BuyingListPage() {
  const scope = useLocationScope();
  return (
    <>
      <PageHeader
        title="Buying list"
        description="What to buy at the harbour: confirmed orders for the delivery date, as raw fish before cleaning, against the stock on hand."
      />
      <InventoryNav />
      {scope.locationId ? (
        <BuyingList locationId={scope.locationId} />
      ) : (
        <LocationRequired action="see the buying list" />
      )}
    </>
  );
}

/** Packs are stocked as PIECE (one unit per pack); show them as packs. */
function unitOf(item: BuyingListItem): QuantityUnit {
  return item.sellUnit === "PACK" ? "PACK" : item.stockUnit;
}

function qty(value: number | null, unit: QuantityUnit): string {
  return value == null ? "—" : formatQuantity({ value, unit });
}

function grams(value: number | null): string {
  return value == null ? "—" : formatWeight(value);
}

function summaryOf(data: BuyingListResponse): string {
  const orders = data.ordersCount === 1 ? "order" : "orders";
  const parts = [`${data.ordersCount} confirmed ${orders}`];
  if (data.safetyMarginBp > 0) {
    parts.push(`safety margin ${formatPercentFromBp(data.safetyMarginBp)}`);
  }
  parts.push(`raw fish to buy ${formatWeight(data.estimatedRawGrams)}`);
  parts.push(`short ${formatWeight(data.shortfallRawGrams)}`);
  return parts.join(" · ");
}

function pendingText(count: number): string {
  const orders = count === 1 ? "order is" : "orders are";
  return `${count} online ${orders} still waiting for payment and not counted.`;
}

/** "Curry cut: 1.5 kg · yield 65% → 2.309 kg raw"; packs add their net weight. */
function preparationText(item: BuyingListItem, p: BuyingListPreparation): string {
  let text = `${p.preparationName || p.preparationCode}: ${qty(p.orderedQuantity, unitOf(item))}`;
  if (item.stockUnit === "PIECE" && p.orderedNetGrams != null) {
    text += ` (${formatWeight(p.orderedNetGrams)} net)`;
  }
  if (p.estimatedRawGrams != null) {
    text += ` · yield ${formatPercentFromBp(p.yieldBp)} → ${formatWeight(p.estimatedRawGrams)} raw`;
  }
  return text;
}

function BuyingList({ locationId }: { locationId: string }) {
  const [date, setDate] = useState("");
  const [marginText, setMarginText] = useState("");
  const marginBp = marginText.trim() === "" ? 0 : parsePercentToBp(marginText);
  const marginError =
    marginBp === null || marginBp > 10_000
      ? "Enter a safety margin between 0 and 100 %."
      : undefined;
  const query: BuyingListQuery = {
    fulfillmentLocationId: locationId,
    ...(date ? { deliveryDate: date } : {}),
    ...(marginBp ? { safetyMarginBp: marginBp } : {}),
  };
  const list = useBuyingList(marginError ? null : query);
  const csv = useBuyingListCsv();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-4 print:hidden">
        <FormField label="Delivery date" hint="Empty = today at this location.">
          <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </FormField>
        <FormField
          label="Safety margin (%)"
          hint="Extra to buy on top of the orders. Not saved."
          error={marginError}
        >
          <Input
            inputMode="decimal"
            value={marginText}
            placeholder="0"
            onChange={(e) => setMarginText(e.target.value)}
          />
        </FormField>
        <div className="flex gap-2">
          <Button
            variant="outline"
            leadingIcon={<Printer aria-hidden="true" />}
            disabled={!list.data}
            onClick={() => window.print()}
          >
            Print
          </Button>
          <Button
            variant="outline"
            leadingIcon={<Download aria-hidden="true" />}
            disabled={!list.data || Boolean(marginError)}
            loading={csv.isPending}
            onClick={() =>
              csv.mutate(query, {
                onError: (error) => reportError(error, { title: "Download failed" }),
              })
            }
          >
            Download CSV
          </Button>
        </div>
      </div>
      {!marginError && (
        <QueryView query={list} what="buying list">
          {(data) => (
            <div className="fo-print-area flex flex-col gap-4">
              <header className="flex flex-col gap-1">
                <h2 className="text-lg font-semibold">
                  {`${data.locationName} · delivery ${formatCalendarDate(data.deliveryDate)}`}
                </h2>
                <p className="text-text-muted text-sm">{summaryOf(data)}</p>
                {data.pendingPaymentOrders > 0 && (
                  <p className="text-warning-text text-sm">
                    {pendingText(data.pendingPaymentOrders)}
                  </p>
                )}
              </header>
              {data.items.length === 0 ? (
                <ListEmpty
                  title="No confirmed orders for this date"
                  description="Orders appear here once they are confirmed for a slot on the delivery date."
                />
              ) : (
                <BuyingTable items={data.items} />
              )}
            </div>
          )}
        </QueryView>
      )}
    </div>
  );
}

function BuyingTable({ items }: { items: BuyingListItem[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="sr-only">Buying list</caption>
        <thead>
          <tr className="text-text-muted border-border border-b text-left">
            <th scope="col" className="py-2 pr-3 font-medium">
              Product / variant
            </th>
            <th scope="col" className="py-2 pr-3 font-medium">
              Orders by preparation
            </th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">
              Ordered (net)
            </th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">
              Need with margin
            </th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">
              In stock
            </th>
            <th scope="col" className="py-2 text-right font-medium">
              To buy
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <BuyingRow key={item.variantId} item={item} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function BuyingRow({ item }: { item: BuyingListItem }) {
  const unit = unitOf(item);
  const showRaw = item.stockUnit === "PIECE";
  return (
    <tr className="border-border border-b align-top" data-testid={`buy-${item.sku}`}>
      <th scope="row" className="py-2 pr-3 text-left font-medium">
        {`${item.productName} · ${item.variantName}`}
        <span className="text-text-muted ml-1.5 font-mono text-xs">{item.sku}</span>
      </th>
      <td className="py-2 pr-3">
        <ul className="flex flex-col gap-0.5">
          {item.preparations.map((p) => (
            <li key={`${p.preparationCode}-${p.yieldBp}`}>{preparationText(item, p)}</li>
          ))}
        </ul>
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.orderedNetGrams != null
          ? grams(item.orderedNetGrams)
          : qty(item.orderedQuantity, unit)}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {qty(item.withMarginQuantity, unit)}
        {showRaw && item.withMarginRawGrams != null && (
          <span className="text-text-muted block text-xs">
            {`${grams(item.withMarginRawGrams)} raw`}
          </span>
        )}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.stockItemMissing ? (
          <Badge tone="warning">No stock item</Badge>
        ) : (
          qty(item.availableQuantity, unit)
        )}
        {!item.stockItemMissing && item.reservedForOtherOrders > 0 && (
          <span className="text-text-muted block text-xs">
            {`of ${qty(item.onHand, unit)} on hand`}
          </span>
        )}
      </td>
      <td className="py-2 text-right font-semibold tabular-nums">
        {item.shortfallQuantity > 0 ? (
          qty(item.shortfallQuantity, unit)
        ) : (
          <Badge tone="success">Enough</Badge>
        )}
        {showRaw && item.shortfallQuantity > 0 && item.shortfallRawGrams != null && (
          <span className="text-text-muted block text-xs font-normal">
            {`${grams(item.shortfallRawGrams)} raw`}
          </span>
        )}
      </td>
    </tr>
  );
}
