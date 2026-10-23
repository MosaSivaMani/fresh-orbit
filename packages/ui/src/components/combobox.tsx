import { Command } from "cmdk";
import { Check, ChevronsUpDown } from "lucide-react";
import { Popover } from "radix-ui";
import { useId, useState } from "react";
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
  ...rest
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const listId = useId();
  const {
    id,
    disabled,
    required: _required,
    ...aria
  } = useFormFieldControl({ ...rest, required: undefined });
  const selected = options.find((o) => o.value === value);

  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
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
            !selected && "text-text-muted",
            className,
          )}
          {...aria}
        >
          <span className="truncate">{selected ? selected.label : placeholder}</span>
          <ChevronsUpDown aria-hidden="true" className="text-text-muted size-4 shrink-0" />
        </button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={4}
          className="border-border bg-surface-raised text-text z-[1000] w-(--radix-popover-trigger-width) min-w-56 rounded-md border p-0 shadow-lg"
        >
          <Command loop className="flex flex-col">
            <Command.Input
              placeholder={searchPlaceholder}
              aria-label={searchPlaceholder}
              className="border-border placeholder:text-text-muted h-10 border-b bg-transparent px-3 text-sm outline-none"
            />
            <Command.List id={listId} className="max-h-64 overflow-y-auto p-1">
              <Command.Empty className="text-text-muted px-2 py-6 text-center text-sm">
                {emptyText}
              </Command.Empty>
              {options.map((option) => (
                <Command.Item
                  key={option.value}
                  value={option.value}
                  keywords={[option.label, ...(option.keywords ?? [])]}
                  {...(option.disabled ? { disabled: true } : {})}
                  onSelect={() => {
                    onValueChange(clearable && option.value === value ? null : option.value);
                    setOpen(false);
                  }}
                  className="data-[selected=true]:bg-muted flex cursor-pointer items-center gap-2 rounded-sm px-2 py-2 text-sm select-none data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-50"
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
          </Command>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
