"use client";

import {
  Button,
  Checkbox,
  FilterChip,
  FormField,
  Input,
  Select,
  Sheet,
  SheetContent,
  SheetTrigger,
} from "@fresh/ui";
import { SlidersHorizontal, X } from "lucide-react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { type FormEvent, useState, useTransition } from "react";
import { type CategoryRef } from "@/api/types/catalog";
import { SORT_OPTIONS, type ShopFilters as Filters } from "@/lib/shop-params";
import { sourceTypeLabels } from "@/lib/status-maps";

export interface ShopFiltersProps {
  filters: Filters;
  /** Top-level categories for the category filter; omitted on a category page. */
  categories?: CategoryRef[];
  hasLocation: boolean;
}

function useUrlUpdater() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [pending, startTransition] = useTransition();
  const update = (change: (params: URLSearchParams) => void) => {
    const params = new URLSearchParams(searchParams.toString());
    change(params);
    params.delete("page");
    const qs = params.toString();
    startTransition(() => router.push(qs ? `${pathname}?${qs}` : pathname, { scroll: false }));
  };
  return { update, pending };
}

function toggle(params: URLSearchParams, key: string, value: string, on: boolean) {
  const values = params.getAll(key).filter((v) => v !== value);
  params.delete(key);
  for (const v of on ? [...values, value] : values) params.append(key, v);
}

function FilterPanel({ filters, categories, hasLocation }: ShopFiltersProps) {
  const { update } = useUrlUpdater();
  const [min, setMin] = useState(filters.minRupees?.toString() ?? "");
  const [max, setMax] = useState(filters.maxRupees?.toString() ?? "");

  function applyPrice(event: FormEvent) {
    event.preventDefault();
    update((p) => {
      if (/^\d+$/.test(min)) p.set("min", min);
      else p.delete("min");
      if (/^\d+$/.test(max)) p.set("max", max);
      else p.delete("max");
    });
  }

  return (
    <div className="flex flex-col gap-6">
      {categories && categories.length > 0 && (
        <fieldset className="flex flex-col gap-2.5">
          <legend className="text-text mb-2 text-sm font-semibold">Category</legend>
          {categories.map((c) => (
            <Checkbox
              key={c.slug}
              label={c.name}
              checked={filters.categories.includes(c.slug)}
              onCheckedChange={(checked) =>
                update((p) => toggle(p, "category", c.slug, checked === true))
              }
            />
          ))}
        </fieldset>
      )}
      <form onSubmit={applyPrice} className="flex flex-col gap-3">
        <fieldset disabled={!hasLocation} className="flex flex-col gap-3">
          <legend className="text-text mb-1 text-sm font-semibold">Price (₹ per unit)</legend>
          {!hasLocation && (
            <p className="text-text-muted text-xs">Set your pincode to filter by price.</p>
          )}
          <div className="grid grid-cols-2 gap-2">
            <FormField label="Min">
              <Input
                inputMode="numeric"
                value={min}
                onChange={(e) => setMin(e.target.value.replace(/\D/g, ""))}
              />
            </FormField>
            <FormField label="Max">
              <Input
                inputMode="numeric"
                value={max}
                onChange={(e) => setMax(e.target.value.replace(/\D/g, ""))}
              />
            </FormField>
          </div>
          <Button type="submit" variant="outline" size="sm">
            Apply price
          </Button>
        </fieldset>
      </form>
    </div>
  );
}

/** "More filters" chip opening a bottom sheet with categories and price (same on phone and desktop). */
export function ShopFilters(props: ShopFiltersProps) {
  return (
    <Sheet>
      <SheetTrigger asChild>
        <FilterChip icon={<SlidersHorizontal aria-hidden="true" />}>More filters</FilterChip>
      </SheetTrigger>
      <SheetContent side="bottom" title="Filters">
        <FilterPanel {...props} />
      </SheetContent>
    </Sheet>
  );
}

export interface ActiveChip {
  key: string;
  value?: string;
  label: string;
}

/** The app's filter chip row: sort, source and stock chips, more filters, then removable active filters. */
export function ShopToolbar({
  chips,
  filters,
  hasLocation,
  hasQuery,
  categories,
}: {
  chips: ActiveChip[];
  filters: Filters;
  hasLocation: boolean;
  hasQuery: boolean;
  categories?: CategoryRef[];
}) {
  const { update, pending } = useUrlUpdater();
  const options = SORT_OPTIONS.filter((o) => !o.needsQuery || hasQuery).map((o) => ({
    value: o.value,
    label: o.needsLocation && !hasLocation ? `${o.label} (set pincode)` : o.label,
    ...(o.needsLocation && !hasLocation ? { disabled: true } : {}),
  }));
  const removable = chips.filter((c) => c.key !== "source" && c.key !== "inStock");
  return (
    <div className="flex flex-col gap-3" aria-busy={pending || undefined}>
      <div className="-mx-4 flex items-center gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:px-0">
        <Select
          aria-label="Sort by"
          className="border-border bg-surface-paper h-9 w-auto shrink-0 rounded-full border px-3.5 font-semibold shadow-none"
          value={filters.sort ?? "default"}
          options={options}
          onValueChange={(value) =>
            update((p) => (value === "default" ? p.delete("sort") : p.set("sort", value)))
          }
        />
        {(["WILD", "FARMED"] as const).map((source) => {
          const on = filters.sourceTypes.includes(source);
          return (
            <FilterChip
              key={source}
              pressed={on}
              onClick={() => update((p) => toggle(p, "source", source, !on))}
            >
              {sourceTypeLabels[source]}
            </FilterChip>
          );
        })}
        <FilterChip
          pressed={filters.inStock}
          onClick={() =>
            update((p) => (filters.inStock ? p.delete("inStock") : p.set("inStock", "true")))
          }
        >
          In stock
        </FilterChip>
        <ShopFilters
          filters={filters}
          hasLocation={hasLocation}
          {...(categories ? { categories } : {})}
        />
      </div>
      {removable.length > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Active filters">
          {removable.map((chip) => (
            <li key={`${chip.key}:${chip.value ?? ""}`}>
              <FilterChip
                pressed
                onClick={() =>
                  update((p) =>
                    chip.value ? toggle(p, chip.key, chip.value, false) : p.delete(chip.key),
                  )
                }
                aria-label={`Remove filter ${chip.label}`}
              >
                {chip.label}
                <X aria-hidden="true" />
              </FilterChip>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
