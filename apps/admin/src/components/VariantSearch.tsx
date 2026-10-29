import { Button, Combobox } from "@fresh/ui";
import { useState } from "react";
import { useVariantById, useVariantSearch } from "../api/catalog";
import { type VariantLookup } from "../api/types/catalog";
import { useDebouncedValue } from "../lib/debounce";

export function variantLabel(v: VariantLookup): string {
  return `${v.productName} · ${v.variantName} (${v.sku})`;
}

/**
 * Picks an active variant from the staff-readable lookup (G3) with the search done on the server (AD-4): the typed
 * text goes as `q` after a short pause, results come a page at a time ("Show more"), and the chosen variant keeps
 * its name from an `id=` read when it is not on the page shown. Use inside a `FormField`.
 */
export function VariantSearch({
  value,
  onChange,
}: {
  value: string;
  onChange: (variant: VariantLookup | null) => void;
}) {
  const [search, setSearch] = useState("");
  const q = useDebouncedValue(search);
  const results = useVariantSearch(q);
  const found = results.data?.pages.flatMap((p) => p.items) ?? [];
  const listed = found.some((v) => v.variantId === value);
  const chosen = useVariantById(value, !listed);
  const known = listed ? found : chosen.data ? [...found, chosen.data] : found;

  return (
    <Combobox
      options={found.map((v) => ({ value: v.variantId, label: variantLabel(v) }))}
      value={value || null}
      {...(chosen.data ? { selectedLabel: variantLabel(chosen.data) } : {})}
      onSearchChange={setSearch}
      loading={results.isFetching && !results.isFetchingNextPage}
      onValueChange={(id) => onChange(known.find((v) => v.variantId === id) ?? null)}
      placeholder={results.isPending ? "Loading variants…" : "Choose a variant"}
      searchPlaceholder="Search product, variant or SKU"
      emptyText="No active variant matches."
      footer={
        results.hasNextPage ? (
          <Button
            size="sm"
            variant="ghost"
            className="w-full"
            loading={results.isFetchingNextPage}
            onClick={() => void results.fetchNextPage()}
          >
            Show more
          </Button>
        ) : undefined
      }
    />
  );
}
