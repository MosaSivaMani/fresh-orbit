import { Combobox, FormField, Input, Select } from "@fresh/ui";
import { useProduct, useProducts } from "../api/catalog";
import { type AdminVariant } from "../api/types/catalog";
import { useCan } from "../app/services";

export interface VariantChoice {
  productId: string | null;
  variantId: string;
  preparationCode: string;
}

/**
 * Pick product → variant → preparation from the admin catalog (`catalog.manage`). Staff without that
 * permission type the variant id and preparation code instead (no staff-readable variant search exists yet;
 * listed as an API gap in docs/design/admin.md).
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
  onVariant?: (variant: AdminVariant | null) => void;
}) {
  const can = useCan();
  const canBrowse = can("catalog.manage");
  const products = useProducts({ status: "ACTIVE", pageSize: 100 }, canBrowse);
  const product = useProduct(value.productId ?? "", canBrowse && Boolean(value.productId));

  if (!canBrowse) {
    return (
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField
          label="Variant id"
          required
          error={errors.variantId}
          hint="Ask a catalog manager for the id."
        >
          <Input
            value={value.variantId}
            onChange={(e) => onChange({ ...value, variantId: e.target.value.trim() })}
          />
        </FormField>
        <FormField label="Preparation code" required error={errors.preparationCode}>
          <Input
            value={value.preparationCode}
            onChange={(e) =>
              onChange({ ...value, preparationCode: e.target.value.trim().toUpperCase() })
            }
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
