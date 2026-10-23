import { zodResolver } from "@hookform/resolvers/zod";
import { Button, FormField, Select } from "@fresh/ui";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router";
import { z } from "zod";
import { useReceiveStock } from "../../api/inventory";
import { type ReceiptResponse, type StockUnit } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { TextField } from "../../components/form";
import { LocationRequired, PageHeader, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import { istLocalToUtcIso, parseRupeesToPaise } from "../../lib/units";
import { qty } from "./maps";
import { InventoryNav } from "./shared";
import { VariantField } from "./VariantField";

const UNIT_OPTIONS = [
  { value: "GRAM", label: "Grams" },
  { value: "PIECE", label: "Pieces" },
];

const schema = z.object({
  variantId: z.string().min(1, "Choose a variant."),
  batchCode: z
    .string()
    .trim()
    .min(1, "Enter the batch code.")
    .max(64, "Batch code must be at most 64 characters."),
  supplierName: z.string().max(200, "Supplier name must be at most 200 characters."),
  sourceLandingCenter: z.string().max(200, "Landing centre must be at most 200 characters."),
  caughtOrHarvestedOn: z.string(),
  receivedQuantity: z
    .string()
    .trim()
    .regex(/^[1-9]\d*$/, "Enter the received quantity as a whole number greater than zero."),
  unit: z.enum(["GRAM", "PIECE"]),
  totalCost: z
    .string()
    .trim()
    .refine(
      (v) => parseRupeesToPaise(v) !== null,
      "Enter the total cost in rupees, for example 1250.50.",
    ),
  bestBeforeAt: z
    .string()
    .refine((v) => istLocalToUtcIso(v) !== null, "Enter the best-before date and time."),
  receiptTemperatureC: z
    .string()
    .trim()
    .refine(
      (v) => v === "" || /^-?\d{1,2}(\.\d)?$/.test(v),
      "Enter the temperature in °C, for example 2.5.",
    ),
});

type Values = z.infer<typeof schema>;

const FIELDS = [
  "variantId",
  "batchCode",
  "supplierName",
  "sourceLandingCenter",
  "caughtOrHarvestedOn",
  "receivedQuantity",
  "totalCost",
  "bestBeforeAt",
  "receiptTemperatureC",
] as const;

const ALIASES = {
  "receivedQuantity.value": "receivedQuantity",
  "receivedQuantity.unit": "unit",
  totalCostPaise: "totalCost",
  fulfillmentLocationId: "variantId",
};

export function ReceiveStockPage() {
  const scope = useLocationScope();
  const [params] = useSearchParams();
  const receive = useReceiveStock();
  const key = useIdempotencyKey();
  const [result, setResult] = useState<ReceiptResponse | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      variantId: params.get("variantId") ?? "",
      batchCode: "",
      supplierName: "",
      sourceLandingCenter: "",
      caughtOrHarvestedOn: "",
      receivedQuantity: "",
      unit: "GRAM",
      totalCost: "",
      bestBeforeAt: "",
      receiptTemperatureC: "",
    },
  });

  if (!scope.locationId) {
    return (
      <>
        <PageHeader title="Receive stock" />
        <InventoryNav />
        <LocationRequired action="receive stock into it" />
      </>
    );
  }
  const locationId = scope.locationId;

  const submit = form.handleSubmit(async (v) => {
    try {
      const response = await receive.mutateAsync({
        idempotencyKey: key.current(),
        body: {
          fulfillmentLocationId: locationId,
          variantId: v.variantId,
          batchCode: v.batchCode.trim(),
          supplierName: v.supplierName.trim() || null,
          sourceLandingCenter: v.sourceLandingCenter.trim() || null,
          caughtOrHarvestedOn: v.caughtOrHarvestedOn || null,
          receivedQuantity: { value: Number(v.receivedQuantity), unit: v.unit as StockUnit },
          totalCostPaise: parseRupeesToPaise(v.totalCost) ?? 0,
          bestBeforeAt: istLocalToUtcIso(v.bestBeforeAt) ?? "",
          receiptTemperatureC: v.receiptTemperatureC === "" ? null : Number(v.receiptTemperatureC),
        },
      });
      key.reset();
      setResult(response);
      notifySaved(`Batch ${response.batch.batchCode} received`);
      form.reset({
        ...form.getValues(),
        batchCode: "",
        receivedQuantity: "",
        totalCost: "",
        receiptTemperatureC: "",
      });
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        aliases: ALIASES,
        title: "Could not receive stock",
      });
    }
  });

  return (
    <>
      <PageHeader
        title="Receive stock"
        description={`Record a batch arriving at ${scope.nameOf(locationId)}. The server values the stock and updates on-hand.`}
      />
      <InventoryNav />
      <div className="flex max-w-3xl flex-col gap-6">
        {result && (
          <div
            role="status"
            className={`flex flex-col gap-1 rounded-lg border p-4 text-sm ${
              result.temperatureWarning
                ? "bg-warning-subtle text-warning-text border-transparent"
                : "bg-success-subtle text-success-text border-transparent"
            }`}
          >
            <p className="font-semibold">
              {result.temperatureWarning
                ? `Batch ${result.batch.batchCode} received above 4 °C`
                : `Batch ${result.batch.batchCode} received`}
            </p>
            {result.temperatureWarning && (
              <p>
                The receipt was accepted and an out-of-range cold-chain reading was logged. Check
                the batch quality.
              </p>
            )}
            <p>
              {qty(result.batch.receivedQuantity)} added; available now{" "}
              {qty(result.stockItem.available)}.{" "}
              <Link className="underline" to={`/inventory/stock/${result.stockItem.id}`}>
                Open the stock item
              </Link>
            </p>
          </div>
        )}
        <form
          noValidate
          onSubmit={(e) => {
            void submit(e);
          }}
          onChange={() => key.reset()}
          className="flex flex-col gap-6"
        >
          <Section title="What arrived">
            <Controller
              control={form.control}
              name="variantId"
              render={({ field, fieldState }) => (
                <VariantField
                  value={field.value}
                  onChange={(id) => {
                    key.reset();
                    field.onChange(id);
                  }}
                  error={fieldState.error?.message}
                />
              )}
            />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <TextField form={form} name="batchCode" label="Batch code" required />
              <TextField
                form={form}
                name="caughtOrHarvestedOn"
                label="Caught or harvested on"
                type="date"
              />
              <TextField form={form} name="supplierName" label="Supplier" />
              <TextField form={form} name="sourceLandingCenter" label="Landing centre" />
            </div>
          </Section>
          <Section title="Quantity, cost and freshness">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <TextField
                form={form}
                name="receivedQuantity"
                label="Received quantity"
                inputMode="numeric"
                required
              />
              <Controller
                control={form.control}
                name="unit"
                render={({ field, fieldState }) => (
                  <FormField
                    label="Unit"
                    hint="Must match the stock unit of the variant."
                    error={fieldState.error?.message}
                    required
                  >
                    <Select
                      options={UNIT_OPTIONS}
                      value={field.value}
                      onValueChange={(v) => {
                        key.reset();
                        field.onChange(v);
                      }}
                    />
                  </FormField>
                )}
              />
              <TextField
                form={form}
                name="totalCost"
                label="Total cost (₹)"
                hint="What the whole batch cost; the server works out the cost per unit."
                inputMode="decimal"
                required
              />
              <TextField
                form={form}
                name="receiptTemperatureC"
                label="Temperature on receipt (°C)"
                hint="4 °C or colder. Warmer stock is accepted with a cold-chain warning."
                inputMode="decimal"
              />
              <TextField
                form={form}
                name="bestBeforeAt"
                label="Best before (IST)"
                type="datetime-local"
                required
              />
            </div>
          </Section>
          <div className="flex justify-end">
            <Button type="submit" loading={receive.isPending}>
              Receive stock
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}
