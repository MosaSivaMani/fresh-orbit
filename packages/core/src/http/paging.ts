// List conventions (docs/api/README.md §1.5).

/** Every list endpoint returns this envelope. */
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface SortField {
  /** camelCase field name accepted by the endpoint. */
  id: string;
  desc: boolean;
}

export interface PageQuery {
  /** 1-based. */
  page?: number;
  /** Default 20, max 100. */
  pageSize?: number;
  sort?: SortField[];
  q?: string;
}

export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 100;

/** `[{id:"placedAt",desc:true},{id:"orderNumber",desc:false}]` -> `-placedAt,orderNumber`. */
export function toSortParam(sort: readonly SortField[] | undefined): string | undefined {
  if (!sort || sort.length === 0) return undefined;
  return sort.map((s) => (s.desc ? `-${s.id}` : s.id)).join(",");
}

/** Inverse of {@link toSortParam}. */
export function parseSortParam(value: string | null | undefined): SortField[] {
  if (!value) return [];
  return value
    .split(",")
    .map((part) => part.trim())
    .filter((part) => part.length > 0)
    .map((part) =>
      part.startsWith("-") ? { id: part.slice(1), desc: true } : { id: part, desc: false },
    );
}

/** Number of pages for a page envelope (at least 1, so "page 1 of 1" shows for an empty list). */
export function pageCount(page: Pick<Page<unknown>, "pageSize" | "totalCount">): number {
  return Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize)));
}

/** Query parameters for a page request; undefined values are dropped. */
export function pageQueryParams(query: PageQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {};
  if (query.page !== undefined) params.page = query.page;
  if (query.pageSize !== undefined) params.pageSize = Math.min(query.pageSize, MAX_PAGE_SIZE);
  const sort = toSortParam(query.sort);
  if (sort) params.sort = sort;
  if (query.q) params.q = query.q;
  return params;
}
