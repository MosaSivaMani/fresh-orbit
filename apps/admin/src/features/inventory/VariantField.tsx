import { FormField } from "@fresh/ui";
import { VariantSearch } from "../../components/VariantSearch";

/**
 * Picks a variant id by product, variant name or SKU from the staff-readable variant lookup (G3), searched on the
 * server (AD-4), for every staff member whatever their catalogue permission.
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
  return (
    <FormField label={label} required error={error}>
      <VariantSearch value={value} onChange={(v) => onChange(v?.variantId ?? "")} />
    </FormField>
  );
}
