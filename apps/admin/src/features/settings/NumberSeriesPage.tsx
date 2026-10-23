import { zodResolver } from "@hookform/resolvers/zod";
import { formatDateTime, humanizeCode, isApiError } from "@fresh/core";
import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
} from "@fresh/ui";
import { Hash } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useNumberSeries, useSaveNumberSeries } from "../../api/settings";
import { type NumberSeries } from "../../api/types/settings";
import { TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { applyFieldErrors, errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { parseWholeNumber } from "../../lib/units";
import { FormAlert } from "./shared";

export function NumberSeriesPage() {
  const series = useNumberSeries();
  const [editing, setEditing] = useState<NumberSeries | null>(null);
  const rows = series.data ?? [];

  const columns = useMemo<ColumnDef<NumberSeries>[]>(
    () => [
      {
        id: "code",
        header: "Series",
        enableSorting: false,
        cell: ({ row }) => humanizeCode(row.original.code),
      },
      {
        id: "period",
        header: "Period",
        enableSorting: false,
        cell: ({ row }) => row.original.period,
      },
      {
        id: "prefix",
        header: "Prefix",
        enableSorting: false,
        cell: ({ row }) => row.original.prefix,
      },
      {
        id: "next",
        header: "Next number",
        enableSorting: false,
        cell: ({ row }) => <span className="font-mono">{row.original.nextNumberPreview}</span>,
      },
      {
        id: "isInUse",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) =>
          row.original.isInUse ? (
            <Badge tone="info">
              In use
              {row.original.lastIssuedAt
                ? ` · last ${formatDateTime(row.original.lastIssuedAt)}`
                : ""}
            </Badge>
          ) : (
            <Badge>Not used yet</Badge>
          ),
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => (
          <Button
            size="sm"
            variant="ghost"
            aria-label={`Edit ${humanizeCode(row.original.code)} ${row.original.period}`}
            onClick={() => setEditing(row.original)}
          >
            Edit
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <PageHeader
        title="Number series"
        description="Invoice, credit note and order numbers. A series that has issued numbers cannot be renumbered."
      />
      <DataTable
        caption="Number series"
        columns={columns}
        data={rows}
        getRowId={(s) => `${s.code}/${s.period}`}
        page={1}
        pageSize={Math.max(rows.length, 1)}
        totalCount={rows.length}
        onPageChange={() => undefined}
        isLoading={series.isFetching}
        error={tableError(series.error)}
        onRetry={() => void series.refetch()}
        emptyState={<EmptyState icon={<Hash aria-hidden="true" />} title="No number series" />}
      />
      {editing && (
        <SeriesDialog
          series={editing}
          onClose={() => setEditing(null)}
          onConflict={() => {
            setEditing(null);
            void series.refetch();
          }}
        />
      )}
    </>
  );
}

const schema = z.object({
  prefix: z.string().trim().min(1, "Enter the prefix."),
  padWidth: z.string().refine((v) => parseWholeNumber(v) !== null, "Enter the number of digits."),
  nextValue: z.string().refine((v) => parseWholeNumber(v) !== null, "Enter the next number."),
});
type Values = z.infer<typeof schema>;

function SeriesDialog({
  series,
  onClose,
  onConflict,
}: {
  series: NumberSeries;
  onClose: () => void;
  onConflict: () => void;
}) {
  const save = useSaveNumberSeries();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      prefix: series.prefix,
      padWidth: String(series.padWidth),
      nextValue: String(series.nextValue),
    },
  });
  const onSubmit = form.handleSubmit((v) => {
    setFormError(null);
    save.mutate(
      {
        code: series.code,
        period: series.period,
        version: series.version,
        body: {
          prefix: v.prefix,
          padWidth: parseWholeNumber(v.padWidth)!,
          nextValue: parseWholeNumber(v.nextValue)!,
        },
      },
      {
        onSuccess: () => {
          notifySaved("Number series saved");
          onClose();
        },
        onError: (error) => {
          if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT") {
            reportError(error, { onConflict });
            return;
          }
          const unmatched = applyFieldErrors(error, form.setError, [
            "prefix",
            "padWidth",
            "nextValue",
          ]);
          if (!isApiError(error) || Object.keys(error.errors).length === 0)
            setFormError(errorMessage(error));
          else if (unmatched.length > 0) setFormError(unmatched.join(" "));
        },
      },
    );
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={`${humanizeCode(series.code)} · ${series.period}`}
        description={`Next number now: ${series.nextNumberPreview}`}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={save.isPending} onClick={() => void onSubmit()}>
              Save
            </Button>
          </>
        }
      >
        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit();
          }}
        >
          <FormAlert message={formError} />
          <TextField form={form} name="prefix" label="Prefix" required />
          <TextField form={form} name="padWidth" label="Digits" inputMode="numeric" required />
          <TextField form={form} name="nextValue" label="Next value" inputMode="numeric" required />
        </form>
      </DialogContent>
    </Dialog>
  );
}
