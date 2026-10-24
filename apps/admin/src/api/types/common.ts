// Shared API shapes. Every file in this folder aliases the schema generated from the backend OpenAPI document
// (`@fresh/api-client`); regenerate with `pnpm --filter @fresh/api-client fetch-spec && pnpm generate:api`.

import { type Schema } from "@fresh/api-client";

export type { Schema };
export type { Page } from "@fresh/core";

/** UUIDv7 string. */
export type Id = string;
/** ISO-8601 UTC instant with `Z`. */
export type Instant = string;
/** Calendar date `YYYY-MM-DD`. */
export type CalendarDate = string;
/** Time of day `HH:mm` in the location's time zone. */
export type TimeOfDay = string;
/** Row version used for `If-Match` (docs/api §1.7). */
export type Version = string;

export type QuantityUnit = Schema<"CatalogQuantityUnit">;

/** `{ "value": 500, "unit": "GRAM" }` (docs/api §1.1). */
export type Quantity = Schema<"QuantityValue">;

/** Paging + sorting query common to every list (docs/api §1.5). */
export interface ListQuery {
  page?: number;
  pageSize?: number;
  /** `-placedAt,orderNumber`. */
  sort?: string;
  q?: string;
}

/**
 * Some response records carry codes as plain `string` in the OpenAPI document (the C# field is a string; see
 * docs/design/admin.md §4). `Coded<T, { field: Union }>` narrows those fields to the codes the server documents.
 */
export type Coded<T, Codes extends { [K in keyof T]?: unknown }> = Omit<T, keyof Codes> & Codes;

type NullableKeys<T> = { [K in keyof T]-?: null extends T[K] ? K : never }[keyof T];

/**
 * Request records list every field as required, nullable ones included; the API reads a missing field as null.
 * `RequestBody<T>` makes the nullable fields optional so a body sends only what the user set.
 */
export type RequestBody<T> = Omit<T, NullableKeys<T>> & Partial<Pick<T, NullableKeys<T>>>;
