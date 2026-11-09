import {
  formatDate,
  formatDateTime,
  formatPercentFromBp,
  formatRupees,
  isApiError,
  newIdempotencyKey,
} from "@fresh/core";
import { Badge, Button, Checkbox, FormField, Input, RadioGroup } from "@fresh/ui";
import { useState } from "react";
import { useTodaysPrices } from "../../api/buying";
import { useBulkPrices } from "../../api/pricing";
import {
  type TodaysPriceIssue,
  type TodaysPriceItem,
  type TodaysPricesResponse,
} from "../../api/types/buying";
import { type BulkPriceResponse, type BulkPriceRow } from "../../api/types/pricing";
import { useLocationScope } from "../../app/location-scope";
import { LocationRequired, PageHeader, QueryView, Section } from "../../components/page";
import { ListEmpty } from "../../components/states";
import { notifySaved, reportError } from "../../lib/feedback";
import {
  bpToPercentInput,
  paiseToRupeesInput,
  parsePercentToBp,
  parseRupeesToPaise,
} from "../../lib/units";
import { PRICE_UNIT } from "./shared";

const ISSUE_LABEL: Record<TodaysPriceIssue, string> = {
  PRICE_MISSING: "No price here",
  BATCH_MISSING: "No batch received",
  YIELD_MISSING: "No yield",
  TAX_RULE_MISSING: "No GST rule",
};

/** Highest target margin the server accepts (95 %). */
const MAX_TARGET_BP = 9_500;

/**
 * Today's prices (REP-R15, D370–D372): every variant at the location with its current price, the latest batch cost per
 * sell unit after cleaning loss, the margin and a suggested price for the target margin typed on this screen (not
 * stored, D371). Staff type new prices; nothing is applied from a suggestion. "Review" runs the Pricing bulk upload
 * as a dry run (PRC-R13), which shows each change and the large-change flag (PRC-R14); "Save prices" applies the
 * same rows.
 */
export function TodaysPricesPage() {
  const scope = useLocationScope();
  return (
    <>
      <PageHeader
        title="Today's prices"
        description="Price, latest batch cost and margin for every variant at this location. Type new prices, review, then save them together."
        backTo="/pricing"
        backLabel="Prices"
      />
      {scope.locationId ? (
        <TodaysPrices key={scope.locationId} locationId={scope.locationId} />
      ) : (
        <LocationRequired action="update today's prices" />
      )}
    </>
  );
}

/** `categoryId:bp,…` for the query, from the per-category targets that parse. */
function categoryTargetsParam(texts: Record<string, string>): string | undefined {
  const pairs: string[] = [];
  for (const [id, text] of Object.entries(texts)) {
    const bp = parsePercentToBp(text);
    if (bp !== null && bp <= MAX_TARGET_BP) pairs.push(`${id}:${bp}`);
  }
  return pairs.length > 0 ? pairs.join(",") : undefined;
}

function categoriesOf(data: TodaysPricesResponse | undefined): { id: string; name: string }[] {
  const seen = new Map<string, string>();
  for (const item of data?.items ?? []) {
    if (item.categoryId && !seen.has(item.categoryId)) {
      seen.set(item.categoryId, item.categoryName ?? "Category");
    }
  }
  return [...seen].map(([id, name]) => ({ id, name }));
}

function TodaysPrices({ locationId }: { locationId: string }) {
  const [targetText, setTargetText] = useState("");
  const [categoryTexts, setCategoryTexts] = useState<Record<string, string>>({});
  const target = targetText.trim() === "" ? null : parsePercentToBp(targetText);
  const targetError =
    targetText.trim() !== "" && (target === null || target > MAX_TARGET_BP)
      ? "Enter a target margin between 0 and 95 %."
      : undefined;
  const categoryTargets = categoryTargetsParam(categoryTexts);
  const prices = useTodaysPrices({
    fulfillmentLocationId: locationId,
    ...(target !== null && !targetError ? { targetMarginBp: target } : {}),
    ...(categoryTargets ? { categoryTargets } : {}),
  });

  return (
    <div className="flex flex-col gap-4">
      <Section
        title="Target margin"
        description="Used only for the suggested prices on this screen; it is not saved. A category target replaces the screen target for that category."
      >
        <div className="flex flex-wrap items-end gap-4">
          <FormField label="Target margin (%)" error={targetError}>
            <Input
              inputMode="decimal"
              value={targetText}
              placeholder="30"
              onChange={(e) => setTargetText(e.target.value)}
            />
          </FormField>
          {categoriesOf(prices.data).map((c) => (
            <FormField key={c.id} label={`${c.name} (%)`}>
              <Input
                inputMode="decimal"
                value={categoryTexts[c.id] ?? ""}
                placeholder={target !== null ? bpToPercentInput(target) : ""}
                onChange={(e) => setCategoryTexts({ ...categoryTexts, [c.id]: e.target.value })}
              />
            </FormField>
          ))}
        </div>
      </Section>
      <QueryView query={prices} what="price list">
        {(data) => <PriceEditor data={data} onSaved={() => void prices.refetch()} />}
      </QueryView>
    </div>
  );
}

/** Splits a 422's `errors` into messages per variant (`rows[3].unitPricePaise`) and the rest. */
function splitErrors(
  errors: Record<string, { message: string }[]>,
  rows: BulkPriceRow[],
  items: TodaysPriceItem[],
): { byVariant: Record<string, string[]>; general: string[] } {
  const byVariant: Record<string, string[]> = {};
  const general: string[] = [];
  for (const [key, list] of Object.entries(errors)) {
    const index = /^rows\[(\d+)\]/.exec(key)?.[1];
    const sku = index === undefined ? undefined : rows[Number(index)]?.sku;
    const item = items.find((i) => i.sku === sku);
    const messages = list.map((e) => e.message);
    if (item) {
      byVariant[item.variantId] = [...(byVariant[item.variantId] ?? []), ...messages];
    } else {
      general.push(...messages);
    }
  }
  return { byVariant, general };
}

function PriceEditor({ data, onSaved }: { data: TodaysPricesResponse; onSaved: () => void }) {
  const bulk = useBulkPrices();
  const [edits, setEdits] = useState<Record<string, string>>({});
  const [start, setStart] = useState<"now" | "tomorrow">("now");
  const [replaceFuture, setReplaceFuture] = useState(false);
  const [review, setReview] = useState<BulkPriceResponse | null>(null);
  const [applyKey, setApplyKey] = useState<string | null>(null);
  const [rowErrors, setRowErrors] = useState<Record<string, string[]>>({});
  const [generalErrors, setGeneralErrors] = useState<string[]>([]);

  if (data.items.length === 0) {
    return (
      <ListEmpty
        title="No variants to price"
        description="Active variants of products in the catalogue appear here."
      />
    );
  }

  const priceOf = (item: TodaysPriceItem) => parseRupeesToPaise(edits[item.variantId] ?? "");
  const changed = data.items.filter((item) => (edits[item.variantId] ?? "").trim() !== "");
  const invalidSkus = changed
    .filter((item) => priceOf(item) === null)
    .map((item) => item.sku)
    .join(", ");
  const rows: BulkPriceRow[] = [];
  for (const item of changed) {
    const paise = priceOf(item);
    if (paise === null) continue;
    rows.push({
      kind: "PRICE",
      locationCode: data.locationCode,
      sku: item.sku,
      unitPricePaise: paise,
      compareAtPaise: item.compareAtPaise,
    });
  }
  const startOptions = [
    { value: "now", label: "Now" },
    {
      value: "tomorrow",
      label: "Tomorrow, 00:00 at the store",
      description: formatDateTime(data.tomorrowStartsAt, data.timeZone),
    },
  ];

  const resetReview = () => {
    setReview(null);
    setApplyKey(null);
    setRowErrors({});
    setGeneralErrors([]);
  };

  const edit = (variantId: string, text: string) => {
    setEdits({ ...edits, [variantId]: text });
    resetReview();
  };

  const run = (dryRun: boolean) => {
    const body = {
      validFrom: start === "tomorrow" ? data.tomorrowStartsAt : null,
      replaceFuture,
      rows,
    };
    const idempotencyKey = dryRun ? newIdempotencyKey() : (applyKey ?? newIdempotencyKey());
    bulk.mutate(
      { upload: { kind: "json", body }, dryRun, idempotencyKey },
      {
        onSuccess: (result) => {
          setRowErrors({});
          setGeneralErrors([]);
          if (dryRun) {
            setReview(result);
            setApplyKey(newIdempotencyKey());
            return;
          }
          resetReview();
          setEdits({});
          notifySaved(
            "Prices saved",
            `${result.changedCount} of ${result.rowCount} prices changed.`,
          );
          onSaved();
        },
        onError: (error) => {
          if (isApiError(error) && error.status === 422 && Object.keys(error.errors).length > 0) {
            const split = splitErrors(error.errors, rows, data.items);
            resetReview();
            setRowErrors(split.byVariant);
            setGeneralErrors(split.general);
            return;
          }
          reportError(error, { title: dryRun ? "Review failed" : "Prices not saved" });
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <caption className="sr-only">Today's prices</caption>
          <thead>
            <tr className="text-text-muted border-border border-b text-left">
              <th scope="col" className="py-2 pr-3 font-medium">
                Product / variant
              </th>
              <th scope="col" className="py-2 pr-3 text-right font-medium">
                Current price
              </th>
              <th scope="col" className="py-2 pr-3 text-right font-medium">
                Latest cost
              </th>
              <th scope="col" className="py-2 pr-3 text-right font-medium">
                Margin
              </th>
              <th scope="col" className="py-2 pr-3 text-right font-medium">
                Suggested
              </th>
              <th scope="col" className="py-2 font-medium">
                New price (₹)
              </th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((item) => (
              <PriceRow
                key={item.variantId}
                item={item}
                text={edits[item.variantId] ?? ""}
                errors={rowErrors[item.variantId] ?? []}
                onChange={(text) => edit(item.variantId, text)}
              />
            ))}
          </tbody>
        </table>
      </div>
      <Section
        title="Save"
        description="Suggested prices are never applied by themselves; only the new prices typed above are saved."
      >
        <div className="flex flex-wrap items-end gap-6">
          <RadioGroup
            label="Prices start"
            orientation="horizontal"
            options={startOptions}
            value={start}
            onValueChange={(v) => {
              setStart(v === "tomorrow" ? "tomorrow" : "now");
              resetReview();
            }}
          />
          <Checkbox
            label="Replace scheduled prices"
            description="Remove prices scheduled to start after these."
            checked={replaceFuture}
            onCheckedChange={(v) => {
              setReplaceFuture(v === true);
              resetReview();
            }}
          />
        </div>
        {invalidSkus && (
          <p role="alert" className="text-danger-text text-sm">
            {`Type the new price of ${invalidSkus} in rupees, for example 649.50.`}
          </p>
        )}
        {generalErrors.length > 0 && (
          <ul role="alert" className="text-danger-text list-disc pl-5 text-sm">
            {generalErrors.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}
        <div className="flex flex-wrap gap-2">
          <Button
            variant="outline"
            disabled={rows.length === 0 || invalidSkus !== ""}
            loading={bulk.isPending && bulk.variables.dryRun}
            onClick={() => run(true)}
          >
            {`Review ${rows.length} ${rows.length === 1 ? "change" : "changes"}`}
          </Button>
          <Button
            disabled={!review || !applyKey}
            loading={bulk.isPending && !bulk.variables.dryRun}
            onClick={() => run(false)}
          >
            Save prices
          </Button>
        </div>
        {review && <ReviewTable review={review} />}
      </Section>
    </div>
  );
}

function costNote(item: TodaysPriceItem): string | null {
  if (!item.latestBatchReceivedAt) return null;
  let note = `batch ${formatDate(item.latestBatchReceivedAt)}`;
  if (item.sellUnit === "KG" && item.yieldBp != null) {
    note += ` · yield ${formatPercentFromBp(item.yieldBp)}`;
  }
  return note;
}

function PriceRow({
  item,
  text,
  errors,
  onChange,
}: {
  item: TodaysPriceItem;
  text: string;
  errors: string[];
  onChange: (text: string) => void;
}) {
  const unit = PRICE_UNIT[item.sellUnit] ?? item.sellUnit;
  const note = costNote(item);
  return (
    <tr className="border-border border-b align-top" data-testid={`price-${item.sku}`}>
      <th scope="row" className="py-2 pr-3 text-left font-medium">
        {`${item.productName} · ${item.variantName}`}
        <span className="text-text-muted ml-1.5 font-mono text-xs">{item.sku}</span>
        <span className="text-text-muted block text-xs font-normal">
          {`${item.categoryName ?? "No category"} · ${unit}`}
        </span>
        {item.issues.length > 0 && (
          <span className="mt-1 flex flex-wrap gap-1">
            {item.issues.map((issue) => (
              <Badge key={issue} tone="warning">
                {ISSUE_LABEL[issue] ?? issue}
              </Badge>
            ))}
          </span>
        )}
      </th>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.unitPricePaise != null ? formatRupees(item.unitPricePaise) : "—"}
        {item.nextUnitPricePaise != null && item.nextValidFrom && (
          <span className="text-text-muted block text-xs">
            {`${formatRupees(item.nextUnitPricePaise)} from ${formatDate(item.nextValidFrom)}`}
          </span>
        )}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.costPerUnitPaise != null ? formatRupees(item.costPerUnitPaise) : "—"}
        {note && <span className="text-text-muted block text-xs">{note}</span>}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.marginBp != null ? formatPercentFromBp(item.marginBp) : "—"}
      </td>
      <td className="py-2 pr-3 text-right tabular-nums">
        {item.suggestedPricePaise != null ? (
          <span className="flex flex-col items-end gap-1">
            {formatRupees(item.suggestedPricePaise)}
            <Button
              variant="outline"
              size="sm"
              aria-label={`Use the suggested price for ${item.sku}`}
              onClick={() => onChange(paiseToRupeesInput(item.suggestedPricePaise))}
            >
              Use
            </Button>
          </span>
        ) : (
          "—"
        )}
      </td>
      <td className="py-2">
        <Input
          aria-label={`New price for ${item.sku}`}
          inputMode="decimal"
          className="w-28"
          value={text}
          onChange={(e) => onChange(e.target.value)}
        />
        {errors.map((m) => (
          <p key={m} role="alert" className="text-danger-text mt-1 text-xs">
            {m}
          </p>
        ))}
      </td>
    </tr>
  );
}

function ReviewTable({ review }: { review: BulkPriceResponse }) {
  const summary = `${review.changedCount} to change, ${review.unchangedCount} unchanged`;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="text-text-muted text-left text-sm">
          {`Review, nothing saved yet: ${summary}.`}
        </caption>
        <thead>
          <tr className="text-text-muted border-border border-b text-left">
            <th scope="col" className="py-2 font-medium">
              SKU
            </th>
            <th scope="col" className="py-2 font-medium">
              Now
            </th>
            <th scope="col" className="py-2 font-medium">
              New
            </th>
            <th scope="col" className="py-2 text-right font-medium">
              Change
            </th>
          </tr>
        </thead>
        <tbody>
          {review.rows.map((r) => (
            <tr key={r.index} className="border-border border-b">
              <td className="py-2 font-mono text-xs">{r.sku}</td>
              <td className="py-2">
                {r.current?.unitPricePaise != null ? formatRupees(r.current.unitPricePaise) : "—"}
              </td>
              <td className="py-2">
                {r.new.unitPricePaise != null ? formatRupees(r.new.unitPricePaise) : "—"}
              </td>
              <td className="py-2 text-right">
                <span className="inline-flex items-center gap-1">
                  {r.status === "UNCHANGED" ? (
                    <Badge>Unchanged</Badge>
                  ) : r.changeBp != null ? (
                    formatPercentFromBp(r.changeBp)
                  ) : (
                    <Badge tone="info">New</Badge>
                  )}
                  {r.largeChange && <Badge tone="warning">Large change</Badge>}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
