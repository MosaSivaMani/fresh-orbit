import { Combobox, FormField, Input, Select } from "@fresh/ui";
import { useState } from "react";
import { useProduct, useProducts } from "../../api/catalog";
import { useCan } from "../../app/services";

/**
 * Picks a variant id. Staff with `catalog.manage` search products and pick a variant by name; everyone else
 * types or pastes the variant id (inventory has no staff-readable variant search — API gap in admin.md).
 */
export function VariantField({
  value,
  onChange,
  error,
  label = "Variant",
}: {
  value: string;
  onChange: (variantId: string) => void;
  error?: string | undefined;
  label?: string;
}) {
  const can = useCan();
  const canBrowse = can("catalog.manage");
  const [productId, setProductId] = useState<string | null>(null);
  const products = useProducts({ pageSize: 100 }, canBrowse);
  const product = useProduct(productId ?? "", canBrowse && productId !== null);

  if (!canBrowse) {
    return (
      <FormField label={label} required error={error} hint="Paste the variant id from the catalog.">
        <Input value={value} onChange={(e) => onChange(e.target.value.trim())} autoComplete="off" />
      </FormField>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <FormField label="Product" required>
        <Combobox
          options={(products.data?.items ?? []).map((p) => ({
            value: p.id,
            label: p.name,
            keywords: [p.slug],
          }))}
          value={productId}
          onValueChange={(id) => {
            setProductId(id);
            onChange("");
          }}
          placeholder={products.isPending ? "Loading products…" : "Choose a product"}
          searchPlaceholder="Search products"
        />
      </FormField>
      <FormField label={label} required error={error}>
        <Select
          options={(product.data?.variants ?? []).map((v) => ({
            value: v.id,
            label: `${v.name} (${v.sku})`,
          }))}
          value={value}
          onValueChange={onChange}
          placeholder={productId ? "Choose a variant" : "Choose a product first"}
          disabled={!productId}
        />
      </FormField>
    </div>
  );
}
