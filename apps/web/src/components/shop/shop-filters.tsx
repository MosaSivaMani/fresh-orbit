"use client";

import {
  Button,
  Checkbox,
  FormField,
  Input,
  Select,
  Sheet,
  SheetContent,
  SheetTrigger,
  Switch,
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
      <fieldset className="flex flex-col gap-2.5">
        <legend className="text-text mb-2 text-sm font-semibold">Source</legend>
        {(["WILD", "FARMED"] as const).map((source) => (
          <Checkbox
            key={source}
            label={sourceTypeLabels[source]}
            checked={filters.sourceTypes.includes(source)}
            onCheckedChange={(checked) =>
              update((p) => toggle(p, "source", source, checked === true))
            }
          />
        ))}
      </fieldset>
      <Switch
        label="In stock only"
        checked={filters.inStock}
        onCheckedChange={(checked) =>
          update((p) => (checked ? p.set("inStock", "true") : p.delete("inStock")))
        }
      />
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

/** Desktop: left column. Mobile: "Filters" button opening a bottom sheet. */
export function ShopFilters(props: ShopFiltersProps) {
  return (
    <>
      <div className="hidden lg:block">
        <FilterPanel {...props} />
      </div>
      <div className="lg:hidden">
        <Sheet>
          <SheetTrigger asChild>
            <Button variant="outline" leadingIcon={<SlidersHorizontal aria-hidden="true" />}>
              Filters
            </Button>
          </SheetTrigger>
          <SheetContent side="bottom" title="Filters">
            <FilterPanel {...props} />
          </SheetContent>
        </Sheet>
      </div>
    </>
  );
}

export interface ActiveChip {
  key: string;
  value?: string;
  label: string;
}

/** Active filter chips and the sort control, above the grid. */
export function ShopToolbar({
  chips,
  sort,
  hasLocation,
  hasQuery,
}: {
  chips: ActiveChip[];
  sort: string | undefined;
  hasLocation: boolean;
  hasQuery: boolean;
}) {
  const { update, pending } = useUrlUpdater();
  const options = SORT_OPTIONS.filter((o) => !o.needsQuery || hasQuery).map((o) => ({
    value: o.value,
    label: o.needsLocation && !hasLocation ? `${o.label} (set pincode)` : o.label,
    ...(o.needsLocation && !hasLocation ? { disabled: true } : {}),
  }));
  return (
    <div
      className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"
      aria-busy={pending || undefined}
    >
      <ul className="flex flex-wrap gap-2" aria-label="Active filters">
        {chips.map((chip) => (
          <li key={`${chip.key}:${chip.value ?? ""}`}>
            <button
              type="button"
              onClick={() =>
                update((p) =>
                  chip.value ? toggle(p, chip.key, chip.value, false) : p.delete(chip.key),
                )
              }
              className="border-border bg-surface text-text hover:bg-muted inline-flex items-center gap-1 rounded-full border px-3 py-1 text-sm"
              aria-label={`Remove filter ${chip.label}`}
            >
              {chip.label}
              <X aria-hidden="true" className="size-3.5" />
            </button>
          </li>
        ))}
      </ul>
      <div className="flex items-center gap-2">
        <span className="text-text-muted text-sm" id="sort-label">
          Sort by
        </span>
        <Select
          aria-label="Sort by"
          className="w-56"
          value={sort ?? "default"}
          options={options}
          onValueChange={(value) =>
            update((p) => (value === "default" ? p.delete("sort") : p.set("sort", value)))
          }
        />
      </div>
    </div>
  );
}
