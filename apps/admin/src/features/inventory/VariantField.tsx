import { Combobox, FormField } from "@fresh/ui";
import { useVariantLookup } from "../../api/catalog";

/**
 * Picks a variant id by product, variant name or SKU from the staff-readable variant lookup (G3), for every staff
 * member whatever their catalogue permission.
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
  const variants = useVariantLookup();

  return (
    <FormField label={label} required error={error}>
      <Combobox
        options={(variants.data?.items ?? []).map((v) => ({
          value: v.variantId,
          label: `${v.productName} · ${v.variantName} (${v.sku})`,
          keywords: [v.sku],
        }))}
        value={value || null}
        onValueChange={(id) => onChange(id ?? "")}
        placeholder={variants.isPending ? "Loading variants…" : "Choose a variant"}
        searchPlaceholder="Search product, variant or SKU"
      />
    </FormField>
  );
}
