import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, FormField, IconButton, Input, Select } from "@fresh/ui";
import { Plus, Trash2 } from "lucide-react";
import { Controller, useFieldArray, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { useAddVariant, usePreparationMethods, useUpdateVariant } from "../../api/catalog";
import { type AdminProduct, type AdminVariant, type VariantRequest } from "../../api/types/catalog";
import { SelectField, SwitchField, TextField } from "../../components/form";
import { reportError, notifySaved } from "../../lib/feedback";
import { bpToPercentInput, parsePercentToBp } from "../../lib/units";

const wholeOrEmpty = z.string().regex(/^\d*$/, "Enter a whole number or leave it empty.");

const schema = z.object({
  sku: z.string().trim().min(1, "Enter the SKU."),
  name: z.string().trim().min(1, "Enter the variant name."),
  sellUnit: z.enum(["KG", "PIECE", "PACK"]),
  packSizeGrams: wholeOrEmpty,
  avgPieceGrams: wholeOrEmpty,
  minOrder: wholeOrEmpty,
  maxOrder: wholeOrEmpty,
  orderStep: wholeOrEmpty,
  isActive: z.boolean(),
  defaultIndex: z.string(),
  preparations: z
    .array(
      z.object({
        code: z.string().min(1, "Choose a preparation."),
        yieldPercent: z.string().refine((v) => {
          const bp = parsePercentToBp(v);
          return bp !== null && bp >= 1 && bp <= 10000;
        }, "Enter a yield between 0.01 and 100 %."),
      }),
    )
    .min(1, "Add at least one preparation."),
});

type VariantValues = z.infer<typeof schema>;

const FIELDS = [
  "sku",
  "name",
  "sellUnit",
  "packSizeGrams",
  "avgPieceGrams",
  "minOrder",
  "maxOrder",
  "orderStep",
  "isActive",
  "preparations",
] as const;

function toValues(variant: AdminVariant | null): VariantValues {
  const defaultIndex = variant ? variant.preparations.findIndex((p) => p.isDefault) : 0;
  return {
    sku: variant?.sku ?? "",
    name: variant?.name ?? "",
    sellUnit: variant?.sellUnit ?? "KG",
    packSizeGrams: variant?.packSizeGrams == null ? "" : String(variant.packSizeGrams),
    avgPieceGrams: variant?.avgPieceGrams == null ? "" : String(variant.avgPieceGrams),
    minOrder: variant ? String(variant.minOrder) : "",
    maxOrder: variant ? String(variant.maxOrder) : "",
    orderStep: variant ? String(variant.orderStep) : "",
    isActive: variant?.isActive ?? true,
    defaultIndex: String(Math.max(0, defaultIndex)),
    preparations: variant?.preparations.map((p) => ({
      code: p.code,
      yieldPercent: bpToPercentInput(p.yieldBp),
    })) ?? [{ code: "", yieldPercent: "" }],
  };
}

const intOrNull = (s: string) => (s === "" ? null : Number(s));

function toRequest(values: VariantValues): VariantRequest {
  return {
    sku: values.sku.trim(),
    name: values.name.trim(),
    sellUnit: values.sellUnit,
    packSizeGrams: intOrNull(values.packSizeGrams),
    avgPieceGrams: intOrNull(values.avgPieceGrams),
    minOrder: intOrNull(values.minOrder),
    maxOrder: intOrNull(values.maxOrder),
    orderStep: intOrNull(values.orderStep),
    isActive: values.isActive,
    preparations: values.preparations.map((p, i) => ({
      code: p.code,
      yieldBp: parsePercentToBp(p.yieldPercent) ?? 0,
      isDefault: String(i) === values.defaultIndex,
    })),
  };
}

/** Add or edit one variant with its preparations (yield and the single default). */
export function VariantDialog({
  product,
  variant,
  open,
  onOpenChange,
  onConflict,
}: {
  product: AdminProduct;
  /** null = add a new variant. */
  variant: AdminVariant | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const add = useAddVariant(product.id);
  const update = useUpdateVariant(product.id);
  const methods = usePreparationMethods(open);
  const form = useForm<VariantValues>({ resolver: zodResolver(schema), values: toValues(variant) });
  const preps = useFieldArray({ control: form.control, name: "preparations" });
  const sellUnit = useWatch({ control: form.control, name: "sellUnit" });
  const unitWord = sellUnit === "KG" ? "grams" : sellUnit === "PIECE" ? "pieces" : "packs";

  const aliases = Object.fromEntries(
    preps.fields.flatMap((_, i) => [
      [`preparations[${i}].yieldBp`, `preparations.${i}.yieldPercent`],
      [`preparations[${i}].isDefault`, "preparations"],
    ]),
  );

  const submit = form.handleSubmit(async (values) => {
    try {
      const body = toRequest(values);
      if (variant)
        await update.mutateAsync({ variantId: variant.id, body, version: product.version });
      else await add.mutateAsync({ body, version: product.version });
      notifySaved(variant ? "Variant saved" : "Variant added");
      onOpenChange(false);
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        aliases,
        title: "Variant not saved",
        onConflict: () => {
          onOpenChange(false);
          return onConflict();
        },
      });
    }
  });

  const methodOptions = (methods.data ?? []).map((m) => ({
    value: m.code,
    label: `${m.name} (${m.code})`,
  }));
  const prepError = form.formState.errors.preparations;
  const prepListError = prepError?.message ?? prepError?.root?.message;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        title={variant ? `Edit variant ${variant.sku}` : "Add variant"}
        description="Order limits are in the stock unit: grams for KG variants, otherwise pieces or packs."
      >
        <form noValidate onSubmit={submit} className="flex flex-col gap-5">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <TextField
              form={form}
              name="sku"
              label="SKU"
              required
              disabled={variant !== null}
              hint={variant ? "The SKU cannot change." : undefined}
            />
            <TextField
              form={form}
              name="name"
              label="Name"
              required
              hint="e.g. Medium (3-5 kg fish)"
            />
            <SelectField
              form={form}
              name="sellUnit"
              label="Sell unit"
              required
              disabled={variant !== null}
              options={[
                { value: "KG", label: "Per kg (weight)" },
                { value: "PIECE", label: "Per piece" },
                { value: "PACK", label: "Per pack" },
              ]}
            />
            {sellUnit === "PACK" && (
              <TextField
                form={form}
                name="packSizeGrams"
                label="Pack size (grams)"
                inputMode="numeric"
              />
            )}
            {sellUnit === "PIECE" && (
              <TextField
                form={form}
                name="avgPieceGrams"
                label="Average piece weight (grams)"
                inputMode="numeric"
              />
            )}
            <TextField
              form={form}
              name="minOrder"
              label={`Minimum order (${unitWord})`}
              inputMode="numeric"
              hint={sellUnit === "KG" ? "Empty = server default (250)." : undefined}
            />
            <TextField
              form={form}
              name="maxOrder"
              label={`Maximum order (${unitWord})`}
              inputMode="numeric"
              hint={sellUnit === "KG" ? "Empty = server default (5000)." : undefined}
            />
            <TextField
              form={form}
              name="orderStep"
              label={`Order step (${unitWord})`}
              inputMode="numeric"
              hint={sellUnit === "KG" ? "Empty = server default (250)." : undefined}
            />
          </div>
          <SwitchField
            form={form}
            name="isActive"
            label="Active"
            description="Inactive variants are not sold."
          />

          <fieldset className="flex flex-col gap-3">
            <legend className="mb-1 text-sm font-semibold">Preparations</legend>
            <p className="text-text-muted text-xs">
              Yield is the share of gross weight left after preparation (e.g. steaks 70 %). Exactly
              one preparation is the default.
            </p>
            {preps.fields.map((field, i) => {
              const errors = form.formState.errors.preparations?.[i];
              return (
                <div
                  key={field.id}
                  className="border-border grid grid-cols-1 items-end gap-3 rounded-md border p-3 sm:grid-cols-[1fr_8rem_auto_auto]"
                >
                  <FormField label={`Preparation ${i + 1}`} error={errors?.code?.message} required>
                    <Controller
                      control={form.control}
                      name={`preparations.${i}.code`}
                      render={({ field: f }) => (
                        <Select
                          options={methodOptions}
                          value={f.value}
                          placeholder={methods.isPending ? "Loading…" : "Choose"}
                          onValueChange={(code) => {
                            f.onChange(code);
                            const guidance = methods.data?.find(
                              (m) => m.code === code,
                            )?.guidanceYieldBp;
                            if (
                              guidance != null &&
                              form.getValues(`preparations.${i}.yieldPercent`) === ""
                            ) {
                              form.setValue(
                                `preparations.${i}.yieldPercent`,
                                bpToPercentInput(guidance),
                              );
                            }
                          }}
                        />
                      )}
                    />
                  </FormField>
                  <FormField label="Yield %" error={errors?.yieldPercent?.message} required>
                    <Input
                      inputMode="decimal"
                      {...form.register(`preparations.${i}.yieldPercent`)}
                    />
                  </FormField>
                  <label className="flex h-10 items-center gap-2 text-sm">
                    <input
                      type="radio"
                      value={String(i)}
                      className="accent-accent size-4"
                      {...form.register("defaultIndex")}
                    />
                    Default
                  </label>
                  <IconButton
                    label={`Remove preparation ${i + 1}`}
                    icon={<Trash2 aria-hidden="true" />}
                    disabled={preps.fields.length === 1}
                    onClick={() => {
                      preps.remove(i);
                      const current = Number(form.getValues("defaultIndex"));
                      if (current >= i && current > 0)
                        form.setValue("defaultIndex", String(current - 1));
                    }}
                  />
                </div>
              );
            })}
            {prepListError && (
              <p role="alert" className="text-danger-text text-xs font-medium">
                {prepListError}
              </p>
            )}
            <Button
              variant="outline"
              size="sm"
              className="w-fit"
              leadingIcon={<Plus aria-hidden="true" />}
              onClick={() => preps.append({ code: "", yieldPercent: "" })}
            >
              Add preparation
            </Button>
          </fieldset>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" loading={form.formState.isSubmitting}>
              {variant ? "Save variant" : "Add variant"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
