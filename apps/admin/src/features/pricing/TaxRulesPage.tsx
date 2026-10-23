import { formatDateTime, formatPercentFromBp } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, type ColumnDef, DataTable, Dialog, DialogContent, EmptyState } from "@fresh/ui";
import { Plus } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useScheduleTaxRule, useTaxRules } from "../../api/pricing";
import { type TaxCategoryRules } from "../../api/types/pricing";
import { TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { istLocalToUtcIso, parsePercentToBp } from "../../lib/units";
import { CheckboxField, StartField, hasCode } from "./shared";

export function TaxRulesPage() {
  const rules = useTaxRules();
  const [dialog, setDialog] = useState<{ code: string } | null>(null);
  const data = rules.data ?? [];

  const columns = useMemo<ColumnDef<TaxCategoryRules>[]>(
    () => [
      {
        id: "category",
        header: "Tax category",
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span className="font-medium">{row.original.name}</span>
            <span className="text-text-muted font-mono text-xs">
              {row.original.taxCategoryCode}
            </span>
          </span>
        ),
      },
      {
        id: "current",
        header: "Current rate",
        meta: { align: "right" },
        cell: ({ row }) =>
          row.original.current ? (
            formatPercentFromBp(row.original.current.rateBp)
          ) : (
            <span className="text-text-muted">None</span>
          ),
      },
      {
        id: "next",
        header: "Scheduled",
        cell: ({ row }) =>
          row.original.next
            ? `${formatPercentFromBp(row.original.next.rateBp)} from ${formatDateTime(row.original.next.validFrom)}`
            : "—",
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        meta: { align: "right" },
        cell: ({ row }) => (
          <Button
            variant="outline"
            size="sm"
            aria-label={`New rate for ${row.original.taxCategoryCode}`}
            onClick={() => setDialog({ code: row.original.taxCategoryCode })}
          >
            New rate
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <PageHeader
        title="Tax rules"
        description="GST rate per tax category. Products name their tax category; rates change by scheduling a new period."
        backTo="/pricing"
        backLabel="Prices"
        actions={
          <Button leadingIcon={<Plus aria-hidden="true" />} onClick={() => setDialog({ code: "" })}>
            New tax period
          </Button>
        }
      />
      <DataTable
        caption="Tax rules"
        columns={columns}
        data={data}
        getRowId={(r) => r.taxCategoryCode}
        page={1}
        pageSize={Math.max(data.length, 10)}
        totalCount={data.length}
        onPageChange={() => undefined}
        isLoading={rules.isPending}
        error={tableError(rules.error)}
        onRetry={() => void rules.refetch()}
        emptyState={
          <EmptyState
            title="No tax rules yet"
            description="Schedule the first rate for a tax category."
          />
        }
      />
      {dialog && (
        <TaxRuleDialog
          code={dialog.code}
          onClose={() => setDialog(null)}
          onConflict={() => rules.refetch()}
        />
      )}
    </>
  );
}

const schema = z.object({
  taxCategoryCode: z
    .string()
    .trim()
    .regex(/^[A-Z0-9_]+$/, "Use the UPPER_SNAKE tax category code, e.g. GST_FRESH_FISH."),
  rate: z.string().refine((v) => {
    const bp = parsePercentToBp(v);
    return bp !== null && bp <= 2800;
  }, "Enter a rate between 0 and 28 %."),
  validFrom: z
    .string()
    .refine((v) => v === "" || istLocalToUtcIso(v) !== null, "Enter a valid start date and time."),
  replaceFuture: z.boolean(),
});

type TaxValues = z.infer<typeof schema>;

function TaxRuleDialog({
  code,
  onClose,
  onConflict,
}: {
  code: string;
  onClose: () => void;
  onConflict: () => unknown;
}) {
  const schedule = useScheduleTaxRule();
  const form = useForm<TaxValues>({
    resolver: zodResolver(schema),
    defaultValues: { taxCategoryCode: code, rate: "", validFrom: "", replaceFuture: false },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await schedule.mutateAsync({
        taxCategoryCode: values.taxCategoryCode.trim(),
        rateBp: parsePercentToBp(values.rate) ?? 0,
        validFrom: values.validFrom === "" ? null : istLocalToUtcIso(values.validFrom),
        replaceFuture: values.replaceFuture,
      });
      notifySaved("Tax rate scheduled");
      onClose();
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: ["taxCategoryCode", "rate", "validFrom", "replaceFuture"],
        aliases: { rateBp: "rate" },
        title: "Tax rate not saved",
        onConflict: () => {
          onClose();
          return onConflict();
        },
      });
      if (hasCode(error, "PRICE_PERIOD_OVERLAP")) {
        form.setError("replaceFuture", {
          type: "PRICE_PERIOD_OVERLAP",
          message: "A later rate is already scheduled. Tick this to replace it.",
        });
      }
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent title={code ? `New rate · ${code}` : "New tax period"}>
        <form noValidate onSubmit={submit} className="flex flex-col gap-4">
          <TextField
            form={form}
            name="taxCategoryCode"
            label="Tax category code"
            required
            disabled={code !== ""}
          />
          <TextField
            form={form}
            name="rate"
            label="GST rate (%)"
            required
            inputMode="decimal"
            hint="e.g. 5 or 12."
          />
          <StartField form={form} name="validFrom" />
          <CheckboxField form={form} name="replaceFuture" label="Replace scheduled rates" />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={form.formState.isSubmitting}>
              Schedule rate
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
