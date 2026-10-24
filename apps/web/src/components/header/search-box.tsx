"use client";

import { Input, cn } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { type FormEvent, type KeyboardEvent, useEffect, useId, useState } from "react";
import { browserHttp } from "@/api/browser";
import { suggest } from "@/api/endpoints/catalog";
import { queryKeys } from "@/lib/query-keys";

const MIN_CHARS = 2;

function useDebounced(value: string, ms: number): string {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), ms);
    return () => clearTimeout(timer);
  }, [value, ms]);
  return debounced;
}

/**
 * Search field with server suggestions (`/search/suggest`, trigram matches on names and local names). `label` names
 * the search landmark when a page has more than one; `inputClassName` restyles the field.
 */
export function SearchBox({
  className,
  inputClassName,
  label,
}: {
  className?: string;
  inputClassName?: string;
  label?: string;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const [value, setValue] = useState(params.get("q") ?? "");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const listId = useId();
  const q = useDebounced(value.trim(), 250);

  const suggestions = useQuery({
    queryKey: queryKeys.suggest(q),
    queryFn: ({ signal }) => suggest(browserHttp(), q, signal),
    enabled: q.length >= MIN_CHARS,
    staleTime: 60_000,
  });
  const items = q.length >= MIN_CHARS ? (suggestions.data ?? []) : [];
  const expanded = open && items.length > 0;

  function submit(event: FormEvent) {
    event.preventDefault();
    const term = value.trim();
    setOpen(false);
    if (active >= 0 && items[active]) {
      router.push(`/products/${items[active].slug}`);
      return;
    }
    if (term.length >= MIN_CHARS) router.push(`/search?q=${encodeURIComponent(term)}`);
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (!expanded) return;
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActive((i) => (i + 1) % items.length);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActive((i) => (i <= 0 ? items.length - 1 : i - 1));
    } else if (event.key === "Escape") {
      setOpen(false);
      setActive(-1);
    }
  }

  return (
    <form role="search" aria-label={label} onSubmit={submit} className={className}>
      <div className="relative">
        <label htmlFor={`${listId}-input`} className="sr-only">
          Search fish, prawns, crabs
        </label>
        <Input
          id={`${listId}-input`}
          type="search"
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={expanded}
          aria-controls={listId}
          aria-activedescendant={expanded && active >= 0 ? `${listId}-${active}` : undefined}
          placeholder="Search seer fish, vanjaram, prawns…"
          className={cn(
            "bg-surface-paper border-border h-11 rounded-[14px] border-[1.5px] shadow-none",
            inputClassName,
          )}
          value={value}
          autoComplete="off"
          enterKeyHint="search"
          onChange={(e) => {
            setValue(e.target.value);
            setOpen(true);
            setActive(-1);
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => setTimeout(() => setOpen(false), 150)}
          onKeyDown={onKeyDown}
          startAdornment={<Search aria-hidden="true" className="size-4" />}
        />
        <div
          id={listId}
          role="listbox"
          aria-label="Suggestions"
          hidden={!expanded}
          className="border-border bg-surface-raised absolute inset-x-0 top-full z-[1000] mt-1 overflow-hidden rounded-lg border shadow-lg"
        >
          {items.map((item, index) => (
            <div
              key={item.slug}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={index === active}
              className="aria-selected:bg-muted"
            >
              <Link
                href={`/products/${item.slug}`}
                tabIndex={-1}
                className="hover:bg-muted flex flex-col px-3 py-2 text-sm"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => setOpen(false)}
              >
                <span className="text-text font-medium">{item.name}</span>
                {item.matchedLocalName && (
                  <span className="text-text-muted text-xs">{item.matchedLocalName}</span>
                )}
              </Link>
            </div>
          ))}
        </div>
      </div>
    </form>
  );
}
