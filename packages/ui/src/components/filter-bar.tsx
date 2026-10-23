import { Search, X } from "lucide-react";
import { type ReactNode } from "react";
import { cn, focusRing } from "../lib/cn";
import { Button } from "./button";
import { Input } from "./input";

export interface ActiveFilter {
  id: string;
  /** e.g. "Status: Confirmed". */
  label: string;
  onRemove: () => void;
}

export interface FilterBarProps {
  /** Free-text search (`q`). Debounce in the caller before refetching. */
  search?: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    label?: string;
  };
  /** Filter controls (Select, Combobox, date inputs). */
  children?: ReactNode;
  activeFilters?: readonly ActiveFilter[];
  onClearAll?: () => void;
  /** Right-aligned actions (Export CSV, New product). */
  actions?: ReactNode;
  className?: string;
}

/** Search + filters + active filter chips for admin lists. Keeps filter state in the caller (URL). */
export function FilterBar({
  search,
  children,
  activeFilters = [],
  onClearAll,
  actions,
  className,
}: FilterBarProps) {
  return (
    <div role="search" aria-label="Filters" className={cn("flex flex-col gap-3", className)}>
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
        <div className="flex flex-1 flex-wrap items-end gap-3">
          {search && (
            <div className="w-full sm:w-72">
              <Input
                type="search"
                aria-label={search.label ?? "Search"}
                placeholder={search.placeholder ?? "Search…"}
                value={search.value}
                onChange={(e) => search.onChange(e.target.value)}
                startAdornment={<Search aria-hidden="true" />}
              />
            </div>
          )}
          {children}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {activeFilters.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <ul className="flex flex-wrap items-center gap-2" aria-label="Active filters">
            {activeFilters.map((filter) => (
              <li
                key={filter.id}
                className="border-border bg-muted text-text inline-flex items-center gap-1 rounded-full border py-0.5 pr-1 pl-3 text-xs font-medium"
              >
                {filter.label}
                <button
                  type="button"
                  onClick={filter.onRemove}
                  aria-label={`Remove filter ${filter.label}`}
                  className={cn(
                    "hover:bg-border inline-flex size-6 items-center justify-center rounded-full",
                    focusRing,
                  )}
                >
                  <X aria-hidden="true" className="size-3.5" />
                </button>
              </li>
            ))}
          </ul>
          {onClearAll && (
            <Button variant="link" size="sm" onClick={onClearAll}>
              Clear all
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
