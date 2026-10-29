import { Command } from "cmdk";
import { Check, ChevronsUpDown } from "lucide-react";
import { Popover } from "radix-ui";
import { type ReactNode, useId, useState } from "react";
import { cn } from "../lib/cn";
import { useFormFieldControl } from "./form-field";
import { controlBase } from "./input";

export interface ComboboxOption {
  value: string;
  label: string;
  /** Extra search terms (codes, synonyms such as "vanjaram" for seer fish). */
  keywords?: string[];
  disabled?: boolean;
}

export interface ComboboxProps {
  options: readonly ComboboxOption[];
  value: string | null;
  onValueChange: (value: string | null) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  /** Allow clearing by selecting the current option again. */
  clearable?: boolean;
  disabled?: boolean;
  id?: string;
  "aria-label"?: string;
  "aria-describedby"?: string;
  "aria-invalid"?: boolean;
  className?: string;
  /**
   * Server-side search: called with the typed text, and `options` are shown as given (no filtering in the
   * browser). The caller debounces and fetches.
   */
  onSearchChange?: (search: string) => void;
  /** Shown in the list while the caller is fetching options. */
  loading?: boolean;
  /** Trigger text for a chosen value that is not among the current `options` (server search). */
  selectedLabel?: string;
  /** Rendered under the list, e.g. a "Show more" button for the next page. */
  footer?: ReactNode;
}

/** Searchable single-select (long lists: products, pincodes, staff). Keyboard: type to filter, arrows, Enter. */
export function Combobox({
  options,
  value,
  onValueChange,
  placeholder = "Select…",
  searchPlaceholder = "Search…",
  emptyText = "No results.",
  clearable = false,
  className,
  onSearchChange,
  loading = false,
  selectedLabel,
  footer,
  ...rest
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const listId = useId();
  const {
    id,
    disabled,
    required: _required,
    ...aria
  } = useFormFieldControl({ ...rest, required: undefined });
  const selected = options.find((o) => o.value === value);
  const triggerLabel = selected?.label ?? (value ? selectedLabel : undefined);
  const changeOpen = (next: boolean) => {
    setOpen(next);
    // Each opening starts with an empty search, as before the search became controlled.
    if (!next && search !== "") {
      setSearch("");
      onSearchChange?.("");
    }
  };

  return (
    <Popover.Root open={open} onOpenChange={changeOpen}>
      <Popover.Trigger asChild>
        <button
          type="button"
          id={id}
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-haspopup="listbox"
          disabled={disabled}
          className={cn(
            controlBase,
            "flex h-10 items-center justify-between gap-2 px-3 text-left",
            !triggerLabel && "text-text-muted",
            className,
          )}
          {...aria}
        >
          <span className="truncate">{triggerLabel ?? placeholder}</span>
          <ChevronsUpDown aria-hidden="true" className="text-text-muted size-4 shrink-0" />
        </button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={4}
          className="border-border bg-surface-raised text-text shadow-soft-lg z-[1000] w-(--radix-popover-trigger-width) min-w-56 rounded-xl border p-0"
        >
          <Command loop shouldFilter={!onSearchChange} className="flex flex-col">
            <Command.Input
              placeholder={searchPlaceholder}
              aria-label={searchPlaceholder}
              value={search}
              onValueChange={(text) => {
                setSearch(text);
                onSearchChange?.(text);
              }}
              className="border-border placeholder:text-text-muted h-10 border-b bg-transparent px-3 text-sm outline-none"
            />
            <Command.List id={listId} className="max-h-64 overflow-y-auto p-1">
              {loading && (
                <Command.Loading>
                  <div className="text-text-muted px-2 py-3 text-center text-sm">Loading…</div>
                </Command.Loading>
              )}
              {!loading && (
                <Command.Empty className="text-text-muted px-2 py-6 text-center text-sm">
                  {emptyText}
                </Command.Empty>
              )}
              {options.map((option) => (
                <Command.Item
                  key={option.value}
                  value={option.value}
                  keywords={[option.label, ...(option.keywords ?? [])]}
                  {...(option.disabled ? { disabled: true } : {})}
                  onSelect={() => {
                    onValueChange(clearable && option.value === value ? null : option.value);
                    changeOpen(false);
                  }}
                  className="data-[selected=true]:bg-muted flex cursor-pointer items-center gap-2 rounded-lg px-2 py-2 text-sm select-none data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-50"
                >
                  <Check
                    aria-hidden="true"
                    className={cn(
                      "text-accent size-4",
                      option.value === value ? "opacity-100" : "opacity-0",
                    )}
                  />
                  {option.label}
                </Command.Item>
              ))}
            </Command.List>
            {footer && <div className="border-border border-t p-1">{footer}</div>}
          </Command>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
