import { newIdempotencyKey, formatPercentFromBp, formatRupees, isApiError } from "@fresh/core";
import { Badge, Button, Checkbox, FormField, Input, Textarea } from "@fresh/ui";
import { useState } from "react";
import { useBulkPrices } from "../../api/pricing";
import { BULK_CSV_COLUMNS, type BulkPriceResponse, type BulkValues } from "../../api/types/pricing";
import { PageHeader, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { istLocalToUtcIso } from "../../lib/units";
import { CHARGE_BASIS } from "./shared";

interface RowError {
  row: number;
  field: string;
  message: string;
}

/** Splits a 422's `errors` into per-row errors (`rows[3].sku`) and the rest (`header`, `rows`, `validFrom`). */
function splitErrors(errors: Record<string, { message: string }[]>): {
  rows: RowError[];
  general: string[];
} {
  const rows: RowError[] = [];
  const general: string[] = [];
  for (const [key, list] of Object.entries(errors)) {
    const match = /^rows\[(\d+)\](?:\.(.+))?$/.exec(key);
    for (const e of list) {
      if (match) rows.push({ row: Number(match[1]), field: match[2] ?? "row", message: e.message });
      else general.push(e.message);
    }
  }
  rows.sort((a, b) => a.row - b.row);
  return { rows, general };
}

function valuesText(v: BulkValues | null): string {
  if (!v) return "—";
  if (v.unitPricePaise != null) {
    return v.compareAtPaise != null
      ? `${formatRupees(v.unitPricePaise)} (was ${formatRupees(v.compareAtPaise)})`
      : formatRupees(v.unitPricePaise);
  }
  if (v.chargePaise != null)
    return `${formatRupees(v.chargePaise)} ${v.chargeBasis ? CHARGE_BASIS[v.chargeBasis] : ""}`;
  return "—";
}

export function BulkUploadPage() {
  const bulk = useBulkPrices();
  const [text, setText] = useState("");
  const [validFrom, setValidFrom] = useState("");
  const [replaceFuture, setReplaceFuture] = useState(false);
  const [validFromError, setValidFromError] = useState<string | undefined>();
  const [preview, setPreview] = useState<BulkPriceResponse | null>(null);
  const [applied, setApplied] = useState<BulkPriceResponse | null>(null);
  const [errors, setErrors] = useState<{ rows: RowError[]; general: string[] } | null>(null);
  // One key per apply action, created when a dry run passes and kept for retries of that apply.
  const [applyKey, setApplyKey] = useState<string | null>(null);

  const inputChanged = () => {
    setPreview(null);
    setApplyKey(null);
    setErrors(null);
    setApplied(null);
  };

  const upload = () => {
    const from = validFrom === "" ? null : istLocalToUtcIso(validFrom);
    return { kind: "csv" as const, text, validFrom: from, replaceFuture };
  };

  const run = (dryRun: boolean) => {
    if (validFrom !== "" && istLocalToUtcIso(validFrom) === null) {
      setValidFromError("Enter a valid start date and time.");
      return;
    }
    setValidFromError(undefined);
    const idempotencyKey = dryRun ? newIdempotencyKey() : (applyKey ?? newIdempotencyKey());
    bulk.mutate(
      { upload: upload(), dryRun, idempotencyKey },
      {
        onSuccess: (result) => {
          setErrors(null);
          if (dryRun) {
            setPreview(result);
            setApplyKey(newIdempotencyKey());
          } else {
            setApplied(result);
            setPreview(null);
            setApplyKey(null);
            notifySaved(
              "Prices updated",
              `${result.changedCount} of ${result.rowCount} rows changed.`,
            );
          }
        },
        onError: (error) => {
          if (isApiError(error) && error.status === 422 && Object.keys(error.errors).length > 0) {
            setPreview(null);
            setApplyKey(null);
            setErrors(splitErrors(error.errors));
            return;
          }
          reportError(error, { title: dryRun ? "Dry run failed" : "Prices not updated" });
        },
      },
    );
  };

  return (
    <>
      <PageHeader
        title="Bulk price upload"
        description="Set many prices and preparation charges at once. All rows apply together or none do."
        backTo="/pricing"
        backLabel="Prices"
      />
      <div className="flex flex-col gap-4">
        <Section
          title="1. Rows"
          description={`CSV with the header: ${BULK_CSV_COLUMNS.join(",")}. Amounts are in paise; kind is PRICE or PREP_CHARGE.`}
        >
          <FormField label="CSV file">
            <Input
              type="file"
              accept=".csv,text/csv"
              onChange={async (e) => {
                const file = e.target.files?.[0];
                if (!file) return;
                setText(await file.text());
                inputChanged();
              }}
            />
          </FormField>
          <FormField label="Rows (CSV)" hint="Choose a file above or paste the rows here.">
            <Textarea
              rows={8}
              className="font-mono text-xs"
              value={text}
              placeholder={`${BULK_CSV_COLUMNS.join(",")}\nPRICE,CHN-01,SEER-STEAK-500,64900,,,,,`}
              onChange={(e) => {
                setText(e.target.value);
                inputChanged();
              }}
            />
          </FormField>
          <div className="grid grid-cols-1 items-end gap-4 md:grid-cols-2">
            <FormField
              label="Default start (India time)"
              hint="For rows without valid_from. Empty = now."
              error={validFromError}
            >
              <Input
                type="datetime-local"
                value={validFrom}
                onChange={(e) => {
                  setValidFrom(e.target.value);
                  inputChanged();
                }}
              />
            </FormField>
            <Checkbox
              label="Replace scheduled prices"
              description="Remove prices and charges scheduled to start after these rows."
              checked={replaceFuture}
              onCheckedChange={(v) => {
                setReplaceFuture(v === true);
                inputChanged();
              }}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              disabled={text.trim() === ""}
              loading={bulk.isPending && bulk.variables.dryRun}
              onClick={() => run(true)}
            >
              Dry run
            </Button>
            <Button
              disabled={!preview || !applyKey}
              loading={bulk.isPending && !bulk.variables.dryRun}
              onClick={() => run(false)}
            >
              Apply
            </Button>
          </div>
          {!preview && !applied && !errors && (
            <p className="text-text-muted text-sm">
              Run a dry run first; Apply is enabled once it passes.
            </p>
          )}
        </Section>

        {errors && (
          <Section
            title="Rows refused"
            description="Nothing was saved. Fix these rows and run the dry run again."
          >
            {errors.general.length > 0 && (
              <ul role="alert" className="text-danger-text list-disc pl-5 text-sm">
                {errors.general.map((m) => (
                  <li key={m}>{m}</li>
                ))}
              </ul>
            )}
            {errors.rows.length > 0 && (
              <table className="w-full text-sm">
                <caption className="sr-only">Row errors</caption>
                <thead>
                  <tr className="text-text-muted border-border border-b text-left">
                    <th scope="col" className="py-2 font-medium">
                      Row
                    </th>
                    <th scope="col" className="py-2 font-medium">
                      Field
                    </th>
                    <th scope="col" className="py-2 font-medium">
                      Problem
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {errors.rows.map((e) => (
                    <tr key={`${e.row}-${e.field}-${e.message}`} className="border-border border-b">
                      <td className="py-2">
                        {e.row + 1}{" "}
                        <span className="text-text-muted text-xs">(line {e.row + 2})</span>
                      </td>
                      <td className="py-2 font-mono text-xs">{e.field}</td>
                      <td className="py-2">{e.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Section>
        )}

        {(preview ?? applied) && (
          <ResultSection result={(preview ?? applied) as BulkPriceResponse} />
        )}
      </div>
    </>
  );
}

function ResultSection({ result }: { result: BulkPriceResponse }) {
  return (
    <Section
      title={result.dryRun ? "2. Dry run — nothing saved yet" : "Applied"}
      description={`${result.rowCount} rows · ${result.changedCount} change · ${result.unchangedCount} unchanged`}
    >
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <caption className="sr-only">{result.dryRun ? "Dry run result" : "Applied rows"}</caption>
          <thead>
            <tr className="text-text-muted border-border border-b text-left">
              <th scope="col" className="py-2 font-medium">
                Row
              </th>
              <th scope="col" className="py-2 font-medium">
                Location
              </th>
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
            {result.rows.map((r) => (
              <tr key={r.index} className="border-border border-b">
                <td className="py-2">{r.index + 1}</td>
                <td className="py-2">{r.locationCode}</td>
                <td className="py-2 font-mono text-xs">
                  {r.sku}
                  {r.preparationCode && (
                    <span className="text-text-muted"> · {r.preparationCode}</span>
                  )}
                </td>
                <td className="py-2">{valuesText(r.current)}</td>
                <td className="py-2">{valuesText(r.new)}</td>
                <td className="py-2 text-right">
                  {r.status === "UNCHANGED" ? (
                    <Badge>Unchanged</Badge>
                  ) : (
                    <span className="inline-flex items-center gap-1">
                      {r.changeBp != null ? (
                        formatPercentFromBp(r.changeBp)
                      ) : (
                        <Badge tone="info">New</Badge>
                      )}
                      {r.largeChange && <Badge tone="warning">Large change</Badge>}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  );
}
