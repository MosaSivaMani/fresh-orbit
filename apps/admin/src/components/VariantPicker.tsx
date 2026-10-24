import { Combobox, FormField, Select } from "@fresh/ui";
import { usePreparationMethods, useProduct, useProducts, useVariantLookup } from "../api/catalog";
import { type StockUnit } from "../api/types/catalog";
import { useCan } from "../app/services";

export interface VariantChoice {
  productId: string | null;
  variantId: string;
  preparationCode: string;
}

/** The chosen variant's stock unit, with its order limits when the full catalogue is readable. */
export interface PickedVariant {
  stockUnit: StockUnit;
  minOrder?: number;
  maxOrder?: number;
  orderStep?: number;
}

/**
 * Pick product → variant → preparation from the admin catalog (`catalog.manage`). Staff without that permission
 * pick the variant from the staff-readable lookup and the preparation from every method (G3); the server refuses
 * a preparation the variant does not offer.
 */
export function VariantPicker({
  value,
  onChange,
  errors = {},
  onVariant,
}: {
  value: VariantChoice;
  onChange: (next: VariantChoice) => void;
  errors?: { variantId?: string | undefined; preparationCode?: string | undefined };
  /** Called with the chosen variant (for its stock unit and order step). */
  onVariant?: (variant: PickedVariant | null) => void;
}) {
  const can = useCan();
  const canBrowse = can("catalog.manage");
  const products = useProducts({ status: "ACTIVE", pageSize: 100 }, canBrowse);
  const product = useProduct(value.productId ?? "", canBrowse && Boolean(value.productId));
  const lookup = useVariantLookup(!canBrowse);
  const methods = usePreparationMethods(!canBrowse);

  if (!canBrowse) {
    const found = lookup.data?.items ?? [];
    return (
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField label="Variant" required error={errors.variantId}>
          <Combobox
            options={found.map((v) => ({
              value: v.variantId,
              label: `${v.productName} · ${v.variantName} (${v.sku})`,
              keywords: [v.sku],
            }))}
            value={value.variantId || null}
            placeholder={lookup.isPending ? "Loading variants…" : "Choose a variant"}
            searchPlaceholder="Search product, variant or SKU"
            onValueChange={(id) => {
              const next = found.find((v) => v.variantId === id) ?? null;
              onChange({
                productId: next?.productId ?? null,
                variantId: id ?? "",
                preparationCode: "",
              });
              onVariant?.(next ? { stockUnit: next.stockUnit } : null);
            }}
          />
        </FormField>
        <FormField label="Preparation" required error={errors.preparationCode}>
          <Select
            options={(methods.data ?? []).map((m) => ({ value: m.code, label: m.name }))}
            value={value.preparationCode}
            placeholder={methods.isPending ? "Loading…" : "Choose a preparation"}
            onValueChange={(code) => onChange({ ...value, preparationCode: code })}
          />
        </FormField>
      </div>
    );
  }

  const variants = product.data?.variants.filter((v) => v.isActive) ?? [];
  const variant = variants.find((v) => v.id === value.variantId) ?? null;
  const preparations = variant?.preparations.filter((p) => p.isActive) ?? [];

  return (
    <div className="grid gap-3">
      <FormField label="Product" required>
        <Combobox
          options={(products.data?.items ?? []).map((p) => ({
            value: p.id,
            label: p.name,
            keywords: [p.slug],
          }))}
          value={value.productId}
          placeholder={products.isPending ? "Loading products…" : "Choose a product"}
          searchPlaceholder="Search products"
          onValueChange={(id) => {
            onChange({ productId: id, variantId: "", preparationCode: "" });
            onVariant?.(null);
          }}
        />
      </FormField>
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField label="Variant" required error={errors.variantId} disabled={!value.productId}>
          <Select
            options={variants.map((v) => ({ value: v.id, label: `${v.name} (${v.sku})` }))}
            value={value.variantId}
            placeholder={product.isFetching ? "Loading…" : "Choose a variant"}
            onValueChange={(id) => {
              const next = variants.find((v) => v.id === id) ?? null;
              const preparation =
                next?.preparations.find((p) => p.isDefault && p.isActive)?.code ?? "";
              onChange({ ...value, variantId: id, preparationCode: preparation });
              onVariant?.(next);
            }}
          />
        </FormField>
        <FormField label="Preparation" required error={errors.preparationCode} disabled={!variant}>
          <Select
            options={preparations.map((p) => ({ value: p.code, label: p.name }))}
            value={value.preparationCode}
            placeholder="Choose a preparation"
            onValueChange={(code) => onChange({ ...value, preparationCode: code })}
          />
        </FormField>
      </div>
    </div>
  );
}
