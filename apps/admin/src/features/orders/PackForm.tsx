import { zodResolver } from "@hookform/resolvers/zod";
import { formatQuantity, formatRupees } from "@fresh/core";
import { Badge, Button, Checkbox, FormField, Input } from "@fresh/ui";
import { AlertTriangle, PackageCheck } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { usePackOrder } from "../../api/orders";
import { type AdminOrderDetail, type PackResponse } from "../../api/types/orders";
import { reportError } from "../../lib/feedback";

const schema = z.object({
  lines: z.array(
    z.object({
      orderItemId: z.string(),
      value: z
        .string()
        .trim()
        .min(1, "Enter the weighed quantity (0 if not supplied).")
        .regex(/^\d+$/, "Enter a whole number."),
    }),
  ),
  coldBoxUsed: z.boolean(),
});

type PackValues = z.infer<typeof schema>;

/**
 * Weigh and pack the whole order (docs/api §4.15): every ACTIVE line once, in grams or pieces; 0 marks the
 * line short supplied. The server caps the charge at the max chargeable weight and computes every amount; the
 * form only collects what the scale shows.
 */
export function PackForm({
  order,
  onPacked,
  onConflict,
}: {
  order: AdminOrderDetail & { version: string };
  onPacked: (result: PackResponse) => void;
  onConflict: () => unknown;
}) {
  const pack = usePackOrder(order.id);
  const activeItems = order.items.filter((i) => i.status === "ACTIVE");
  const pendingSubstitution = order.substitutions.some((s) => s.status === "PROPOSED");
  const form = useForm<PackValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      lines: activeItems.map((i) => ({ orderItemId: i.id, value: "" })),
      coldBoxUsed: true,
    },
  });

  const onSubmit = form.handleSubmit((values) => {
    const aliases: Record<string, string> = {};
    activeItems.forEach((_, index) => {
      aliases[`lines[${index}].packedQuantity.value`] = `lines.${index}.value`;
      aliases[`lines[${index}].packedQuantity`] = `lines.${index}.value`;
      aliases[`lines[${index}]`] = `lines.${index}.value`;
    });
    pack.mutate(
      {
        version: order.version,
        body: {
          coldBoxUsed: values.coldBoxUsed,
          lines: values.lines.map((line, index) => ({
            orderItemId: line.orderItemId,
            packedQuantity: {
              value: Number(line.value),
              unit: activeItems[index]!.orderedQuantity.unit,
            },
          })),
        },
      },
      {
        onSuccess: onPacked,
        onError: (error) =>
          reportError(error, {
            setError: form.setError,
            fields: ["lines", "coldBoxUsed"],
            aliases,
            onConflict,
            title: "Could not pack the order",
          }),
      },
    );
  });

  return (
    <form
      noValidate
      onSubmit={onSubmit}
      className="flex flex-col gap-4"
      aria-label="Weigh and pack"
    >
      {pendingSubstitution && (
        <p
          role="status"
          className="bg-warning-subtle text-warning-text flex items-start gap-2 rounded-md px-3 py-2 text-sm"
        >
          <AlertTriangle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />A substitution is
          waiting for the customer. Packing is possible once it is answered or expires.
        </p>
      )}
      <div className="border-border overflow-x-auto rounded-lg border">
        <table className="w-full text-sm">
          <caption className="sr-only">Lines to weigh</caption>
          <thead className="bg-muted/60 text-text-muted text-left text-xs uppercase">
            <tr>
              <th scope="col" className="px-3 py-2">
                #
              </th>
              <th scope="col" className="px-3 py-2">
                Item
              </th>
              <th scope="col" className="px-3 py-2 text-right">
                Ordered
              </th>
              <th scope="col" className="px-3 py-2 text-right">
                Max charged
              </th>
              <th scope="col" className="px-3 py-2">
                Weighed
              </th>
            </tr>
          </thead>
          <tbody>
            {activeItems.map((item, index) => {
              const unitLabel = item.orderedQuantity.unit === "GRAM" ? "g" : "pcs";
              const error = form.formState.errors.lines?.[index]?.value?.message;
              return (
                <tr key={item.id} className="border-border border-t align-top">
                  <td className="px-3 py-3 tabular-nums">{item.lineNo}</td>
                  <td className="px-3 py-3">
                    <span className="block font-medium">{item.productName}</span>
                    <span className="text-text-muted block text-xs">
                      {item.variantName} · {item.preparationName}
                    </span>
                  </td>
                  <td className="px-3 py-3 text-right tabular-nums">
                    {formatQuantity(item.orderedQuantity)}
                  </td>
                  <td className="px-3 py-3 text-right tabular-nums">
                    {formatQuantity(item.maxChargeableQuantity)}
                  </td>
                  <td className="w-48 px-3 py-2">
                    <FormField
                      label={`Weighed quantity for line ${item.lineNo}`}
                      hideLabel
                      required
                      error={error}
                    >
                      <Input
                        inputMode="numeric"
                        autoComplete="off"
                        className="h-12 text-lg tabular-nums"
                        endAdornment={<span className="text-xs">{unitLabel}</span>}
                        {...form.register(`lines.${index}.value`)}
                      />
                    </FormField>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Controller
          control={form.control}
          name="coldBoxUsed"
          render={({ field }) => (
            <Checkbox
              label="Packed in a cold box"
              checked={field.value}
              onCheckedChange={(v) => field.onChange(v === true)}
            />
          )}
        />
        <Button
          type="submit"
          size="lg"
          loading={pack.isPending}
          disabled={pendingSubstitution || activeItems.length === 0}
          leadingIcon={<PackageCheck aria-hidden="true" />}
        >
          Mark packed
        </Button>
      </div>
    </form>
  );
}

/** What the server decided at pack: charged quantities, caps and final amounts. */
export function PackResult({ result, order }: { result: PackResponse; order: AdminOrderDetail }) {
  const nameOf = (id: string) => {
    const item = order.items.find((i) => i.id === id);
    return item ? `${item.lineNo}. ${item.productName}` : id;
  };
  return (
    <div
      role="status"
      className="border-success bg-success-subtle flex flex-col gap-3 rounded-lg border p-4"
    >
      <p className="text-success-text font-semibold">Packed. Final amounts from the server:</p>
      <ul className="flex flex-col gap-1 text-sm">
        {result.lines.map((line) => (
          <li key={line.orderItemId} className="flex flex-wrap items-center gap-2">
            <span className="font-medium">{nameOf(line.orderItemId)}</span>
            <span>
              packed {formatQuantity(line.packed)}, charged {formatQuantity(line.charged)}
            </span>
            {line.capped && <Badge tone="info">Capped at max</Badge>}
            {line.status === "SHORT_SUPPLIED" && <Badge tone="warning">Short supplied</Badge>}
            <span className="ml-auto tabular-nums">{formatRupees(line.finalTotalPaise)}</span>
          </li>
        ))}
      </ul>
      <dl className="grid grid-cols-3 gap-3 text-sm">
        <div>
          <dt className="text-text-muted">Final total</dt>
          <dd className="font-semibold tabular-nums">{formatRupees(result.finalTotalPaise)}</dd>
        </div>
        <div>
          <dt className="text-text-muted">Amount due</dt>
          <dd className="font-semibold tabular-nums">{formatRupees(result.amountDuePaise)}</dd>
        </div>
        <div>
          <dt className="text-text-muted">Refund due</dt>
          <dd className="font-semibold tabular-nums">{formatRupees(result.refundDuePaise)}</dd>
        </div>
      </dl>
    </div>
  );
}
