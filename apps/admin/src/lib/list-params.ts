import { DEFAULT_PAGE_SIZE, MAX_PAGE_SIZE, parseSortParam, toSortParam } from "@fresh/core";
import { type SortingState } from "@fresh/ui";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router";

export type ParamPatch = Readonly<Record<string, string | readonly string[] | null | undefined>>;

export interface ListParams {
  page: number;
  pageSize: number;
  /** API `sort` value (`-placedAt`), or undefined for the server default. */
  sort: string | undefined;
  sorting: SortingState;
  q: string;
  get(key: string): string | undefined;
  getAll(key: string): string[];
  /** Update filters; resets to page 1 unless `page` is in the patch. null/""/[] remove a key. */
  set(patch: ParamPatch): void;
  setPage(page: number): void;
  setPageSize(pageSize: number): void;
  setSorting(sorting: SortingState): void;
  clear(keys: readonly string[]): void;
  /** Query object for the API: paging, sorting, q and the named filters. */
  query(filterKeys: readonly string[]): Record<string, string | number | string[]>;
}

/**
 * List state lives in the URL (docs/design §6.4) so filtered views can be shared, bookmarked and survive
 * reloads. Server paging, sorting and filtering only — the client never filters rows itself.
 */
export function useListParams(
  options: { defaultSort?: string; defaultPageSize?: number } = {},
): ListParams {
  const [params, setParams] = useSearchParams();
  const defaultPageSize = options.defaultPageSize ?? DEFAULT_PAGE_SIZE;

  const page = Math.max(1, Number(params.get("page")) || 1);
  const rawSize = Number(params.get("pageSize")) || defaultPageSize;
  const pageSize = Math.min(Math.max(1, rawSize), MAX_PAGE_SIZE);
  const sort = params.get("sort") ?? options.defaultSort;
  const q = params.get("q") ?? "";

  const set = useCallback(
    (patch: ParamPatch) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(patch)) {
            next.delete(key);
            if (value == null) continue;
            const values = typeof value === "string" ? [value] : value;
            for (const v of values) if (v !== "") next.append(key, v);
          }
          if (!("page" in patch)) next.delete("page");
          return next;
        },
        { replace: true },
      );
    },
    [setParams],
  );

  return useMemo<ListParams>(
    () => ({
      page,
      pageSize,
      sort,
      sorting: parseSortParam(sort),
      q,
      get: (key) => params.get(key) ?? undefined,
      getAll: (key) => params.getAll(key),
      set,
      setPage: (p) => set({ page: p > 1 ? String(p) : null }),
      setPageSize: (s) => set({ pageSize: s === defaultPageSize ? null : String(s) }),
      setSorting: (sorting) => set({ sort: toSortParam(sorting) ?? null }),
      clear: (keys) => set(Object.fromEntries(keys.map((k) => [k, null]))),
      query: (filterKeys) => {
        const result: Record<string, string | number | string[]> = { page, pageSize };
        if (sort) result.sort = sort;
        if (q) result.q = q;
        for (const key of filterKeys) {
          const values = params.getAll(key);
          if (values.length === 1 && values[0] !== undefined) result[key] = values[0];
          else if (values.length > 1) result[key] = values;
        }
        return result;
      },
    }),
    [page, pageSize, sort, q, params, set, defaultPageSize],
  );
}

/** Search box bound to `q` in the URL, debounced so typing does not fire a request per key. */
export function useDebouncedSearch(
  list: Pick<ListParams, "q" | "set">,
  delayMs = 300,
): { value: string; onChange: (value: string) => void } {
  const [value, setValue] = useState(list.q);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const { q, set } = list;

  // Follow the URL when it changes elsewhere (back button, "Clear all").
  const [lastQ, setLastQ] = useState(q);
  if (q !== lastQ) {
    setLastQ(q);
    setValue(q);
  }

  useEffect(() => () => clearTimeout(timer.current), []);

  const onChange = useCallback(
    (next: string) => {
      setValue(next);
      clearTimeout(timer.current);
      timer.current = setTimeout(() => set({ q: next.trim() || null }), delayMs);
    },
    [set, delayMs],
  );

  return { value, onChange };
}
