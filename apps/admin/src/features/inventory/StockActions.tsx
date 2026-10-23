import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent } from "@fresh/ui";
import { type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  useAdjustStock,
  useCountStock,
  useDiscardBatch,
  useRecordWaste,
  useSetThreshold,
} from "../../api/inventory";
import {
  type AdjustmentKind,
  type AdjustmentReason,
  type Batch,
  type StockItem,
  type WasteReason,
} from "../../api/types/inventory";
import { SelectField, TextAreaField, TextField } from "../../components/form";
import { notifySaved, reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import {
  ADJUSTMENT_KIND_OPTIONS,
  ADJUSTMENT_REASON_OPTIONS,
  UNIT_LABEL,
  WASTE_REASON_OPTIONS,
  qty,
} from "./maps";

const wholeNumber = (label: string) =>
  z.string().trim().regex(/^\d+$/, `Enter the ${label} as a whole number.`);
const notes = z.string().max(500, "Notes must be at most 500 characters.");

function ActionDialog({
  open,
  onOpenChange,
  title,
  description,
  submitLabel,
  pending,
  onSubmit,
  danger,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: ReactNode;
  submitLabel: string;
  pending: boolean;
  onSubmit: () => void;
  danger?: boolean;
  children: ReactNode;
}) {
  const formId = `form-${title.replace(/\W+/g, "-").toLowerCase()}`;
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title={title}
        description={description}
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              form={formId}
              loading={pending}
              variant={danger ? "danger" : "primary"}
            >
              {submitLabel}
            </Button>
          </>
        }
      >
        <form
          id={formId}
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSubmit();
          }}
        >
          {children}
        </form>
      </DialogContent>
    </Dialog>
  );
}

function batchOptions(batches: readonly Batch[] | null | undefined) {
  return (batches ?? []).map((b) => ({
    value: b.id,
    label: `${b.batchCode} · ${qty(b.remainingQuantity)} left`,
  }));
}

interface StockDialogProps {
  item: StockItem;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Re-read the stock item after a 412. */
  onConflict: () => unknown;
}

// ---------- Threshold ----------

const thresholdSchema = z.object({ lowStockThreshold: wholeNumber("threshold") });

export function ThresholdDialog({ item, open, onOpenChange, onConflict }: StockDialogProps) {
  const save = useSetThreshold(item.id);
  const form = useForm<z.infer<typeof thresholdSchema>>({
    resolver: zodResolver(thresholdSchema),
    values: { lowStockThreshold: String(item.lowStockThreshold) },
  });
  const submit = form.handleSubmit(async (v) => {
    try {
      await save.mutateAsync({
        lowStockThreshold: Number(v.lowStockThreshold),
        version: item.version,
      });
      notifySaved("Threshold saved");
      onOpenChange(false);
    } catch (error) {
      reportError(error, { onConflict, setError: form.setError, fields: ["lowStockThreshold"] });
    }
  });
  return (
    <ActionDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Low-stock threshold"
      description="The item shows as low stock when available stock is at or below this amount. 0 turns it off."
      submitLabel="Save threshold"
      pending={save.isPending}
      onSubmit={() => void submit()}
    >
      <TextField
        form={form}
        name="lowStockThreshold"
        label={`Threshold (${UNIT_LABEL[item.stockUnit] ?? item.stockUnit})`}
        inputMode="numeric"
        required
      />
    </ActionDialog>
  );
}

// ---------- Adjustment ----------

const adjustSchema = z
  .object({
    kind: z.enum(["ADJUSTMENT_IN", "ADJUSTMENT_OUT", "RETURN"], {
      message: "Choose the kind of adjustment.",
    }),
    batchId: z.string(),
    quantity: wholeNumber("quantity"),
    reasonCode: z.string().min(1, "Choose a reason."),
    notes,
  })
  .superRefine((v, ctx) => {
    if (v.kind === "RETURN" && !v.batchId)
      ctx.addIssue({
        code: "custom",
        path: ["batchId"],
        message: "Choose the batch the stock returns to.",
      });
    if (v.reasonCode === "OTHER" && !v.notes.trim())
      ctx.addIssue({
        code: "custom",
        path: ["notes"],
        message: "Notes are required for the reason Other.",
      });
    if (/^\d+$/.test(v.quantity.trim()) && Number(v.quantity) === 0)
      ctx.addIssue({
        code: "custom",
        path: ["quantity"],
        message: "Quantity must be greater than zero.",
      });
  });

type AdjustValues = z.input<typeof adjustSchema>;

export function AdjustDialog({ item, open, onOpenChange, onConflict }: StockDialogProps) {
  const adjust = useAdjustStock();
  const form = useForm<AdjustValues>({
    resolver: zodResolver(adjustSchema),
    defaultValues: { kind: "ADJUSTMENT_IN", batchId: "", quantity: "", reasonCode: "", notes: "" },
  });
  const submit = form.handleSubmit(async (v) => {
    try {
      await adjust.mutateAsync({
        version: item.version,
        body: {
          stockItemId: item.id,
          kind: v.kind as AdjustmentKind,
          batchId: v.batchId || null,
          quantity: { value: Number(v.quantity), unit: item.stockUnit },
          reasonCode: v.reasonCode as AdjustmentReason,
          notes: v.notes.trim() || null,
        },
      });
      notifySaved("Adjustment recorded");
      form.reset();
      onOpenChange(false);
    } catch (error) {
      reportError(error, {
        onConflict,
        setError: form.setError,
        fields: ["kind", "batchId", "quantity", "reasonCode", "notes"],
        aliases: { "quantity.value": "quantity", "quantity.unit": "quantity" },
      });
    }
  });
  return (
    <ActionDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Adjust stock"
      description={`On hand now: ${qty(item.onHand)}. Every adjustment is recorded as a stock movement.`}
      submitLabel="Record adjustment"
      pending={adjust.isPending}
      onSubmit={() => void submit()}
    >
      <SelectField
        form={form}
        name="kind"
        label="Kind"
        options={ADJUSTMENT_KIND_OPTIONS}
        required
      />
      <SelectField
        form={form}
        name="batchId"
        label="Batch"
        hint="Required for a return; optional otherwise."
        options={batchOptions(item.batches)}
        placeholder="No specific batch"
      />
      <TextField
        form={form}
        name="quantity"
        label={`Quantity (${UNIT_LABEL[item.stockUnit] ?? item.stockUnit})`}
        inputMode="numeric"
        required
      />
      <SelectField
        form={form}
        name="reasonCode"
        label="Reason"
        options={ADJUSTMENT_REASON_OPTIONS}
        required
      />
      <TextAreaField form={form} name="notes" label="Notes" rows={3} />
    </ActionDialog>
  );
}

// ---------- Physical count ----------

const countSchema = z.object({
  countedQuantity: wholeNumber("counted quantity"),
  batchId: z.string(),
  notes,
});

export function CountDialog({ item, open, onOpenChange, onConflict }: StockDialogProps) {
  const count = useCountStock();
  const form = useForm<z.infer<typeof countSchema>>({
    resolver: zodResolver(countSchema),
    defaultValues: { countedQuantity: "", batchId: "", notes: "" },
  });
  const submit = form.handleSubmit(async (v) => {
    try {
      await count.mutateAsync({
        version: item.version,
        body: {
          stockItemId: item.id,
          countedQuantity: { value: Number(v.countedQuantity), unit: item.stockUnit },
          batchId: v.batchId || null,
          notes: v.notes.trim() || null,
        },
      });
      notifySaved("Count recorded", "The server posted a count correction for any difference.");
      form.reset();
      onOpenChange(false);
    } catch (error) {
      reportError(error, {
        onConflict,
        setError: form.setError,
        fields: ["countedQuantity", "batchId", "notes"],
        aliases: { "countedQuantity.value": "countedQuantity" },
      });
    }
  });
  return (
    <ActionDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Record a physical count"
      description="Enter what is physically there. The server records any difference as a count correction."
      submitLabel="Record count"
      pending={count.isPending}
      onSubmit={() => void submit()}
    >
      <SelectField
        form={form}
        name="batchId"
        label="Batch"
        hint="Leave empty to count the whole stock item."
        options={batchOptions(item.batches)}
        placeholder="Whole stock item"
      />
      <TextField
        form={form}
        name="countedQuantity"
        label={`Counted quantity (${UNIT_LABEL[item.stockUnit] ?? item.stockUnit})`}
        inputMode="numeric"
        required
      />
      <TextAreaField form={form} name="notes" label="Notes" rows={3} />
    </ActionDialog>
  );
}

// ---------- Waste ----------

const FEFO = "__fefo";
const wasteSchema = z.object({
  batchId: z.string(),
  quantity: wholeNumber("quantity").refine(
    (v) => Number(v) > 0,
    "Quantity must be greater than zero.",
  ),
  reasonCode: z.string().min(1, "Choose a reason."),
  notes,
});

export function WasteDialog({ item, open, onOpenChange }: Omit<StockDialogProps, "onConflict">) {
  const waste = useRecordWaste();
  const key = useIdempotencyKey();
  const form = useForm<z.infer<typeof wasteSchema>>({
    resolver: zodResolver(wasteSchema),
    defaultValues: { batchId: FEFO, quantity: "", reasonCode: "", notes: "" },
  });
  const submit = form.handleSubmit(async (v) => {
    try {
      await waste.mutateAsync({
        idempotencyKey: key.current(),
        body: {
          stockItemId: item.id,
          batchId: v.batchId && v.batchId !== FEFO ? v.batchId : null,
          quantity: { value: Number(v.quantity), unit: item.stockUnit },
          reasonCode: v.reasonCode as WasteReason,
          notes: v.notes.trim() || null,
        },
      });
      key.reset();
      notifySaved("Waste recorded");
      form.reset();
      onOpenChange(false);
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: ["batchId", "quantity", "reasonCode", "notes"],
        aliases: { "quantity.value": "quantity" },
      });
    }
  });
  return (
    <ActionDialog
      open={open}
      onOpenChange={(o) => {
        if (!o) key.reset();
        onOpenChange(o);
      }}
      title="Record waste"
      description="Spoiled, damaged or rejected stock. Without a batch the server takes the earliest-expiring batches first (FEFO)."
      submitLabel="Record waste"
      pending={waste.isPending}
      onSubmit={() => void submit()}
      danger
    >
      <SelectField
        form={form}
        name="batchId"
        label="Batch"
        options={[
          { value: FEFO, label: "Earliest expiry first (FEFO)" },
          ...batchOptions(item.batches),
        ]}
      />
      <TextField
        form={form}
        name="quantity"
        label={`Quantity (${UNIT_LABEL[item.stockUnit] ?? item.stockUnit})`}
        inputMode="numeric"
        required
      />
      <SelectField
        form={form}
        name="reasonCode"
        label="Reason"
        options={WASTE_REASON_OPTIONS}
        required
      />
      <TextAreaField form={form} name="notes" label="Notes" rows={3} />
    </ActionDialog>
  );
}

// ---------- Discard batch ----------

const discardSchema = z.object({ reasonCode: z.string().min(1, "Choose a reason."), notes });

export function DiscardBatchDialog({
  batch,
  fallbackVersion,
  open,
  onOpenChange,
  onConflict,
}: {
  batch: Batch;
  /** Stock item version when the batch row has none. */
  fallbackVersion?: string | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const discard = useDiscardBatch();
  const form = useForm<z.infer<typeof discardSchema>>({
    resolver: zodResolver(discardSchema),
    defaultValues: { reasonCode: "", notes: "" },
  });
  const version = batch.stockItemVersion ?? fallbackVersion ?? "";
  const submit = form.handleSubmit(async (v) => {
    try {
      await discard.mutateAsync({
        batchId: batch.id,
        version,
        body: { reasonCode: v.reasonCode, notes: v.notes.trim() || null },
      });
      notifySaved(`Batch ${batch.batchCode} discarded`);
      form.reset();
      onOpenChange(false);
    } catch (error) {
      reportError(error, { onConflict, setError: form.setError, fields: ["reasonCode", "notes"] });
    }
  });
  return (
    <ActionDialog
      open={open}
      onOpenChange={onOpenChange}
      title={`Discard batch ${batch.batchCode}`}
      description={`The remaining ${qty(batch.remainingQuantity)} is recorded as waste. This cannot be undone.`}
      submitLabel="Discard batch"
      pending={discard.isPending}
      onSubmit={() => void submit()}
      danger
    >
      <SelectField
        form={form}
        name="reasonCode"
        label="Reason"
        options={WASTE_REASON_OPTIONS}
        required
      />
      <TextAreaField form={form} name="notes" label="Notes" rows={3} />
    </ActionDialog>
  );
}
