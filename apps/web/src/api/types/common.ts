// Shared API shapes, generated from the backend OpenAPI document (`@fresh/api-client`).
// Regenerate with `pnpm --filter @fresh/api-client fetch-spec && pnpm generate:api`.

import { type Schema } from "@fresh/api-client";

export type { Page } from "@fresh/core";

/** `GRAM` for KG variants, `PIECE`, `PACK` (docs/api §1.1). */
export type QuantityUnit = Schema<"QuantityUnit">;

export type Quantity = Schema<"QuantityValue">;

/** Variant sell unit; prices are per this unit. */
export type SellUnit = Schema<"SellUnit">;

export type AvailabilityStatus = Schema<"AvailabilityStatus">;

export type Availability = Schema<"ListAvailability">;

/** A refusal reported inside a 200 response (cart line issues). */
export type Issue = Schema<"CartIssueView">;

/**
 * Some response records carry codes as plain `string` in the OpenAPI document (the C# field is a string; see
 * docs/design/web.md §7). `Coded<T, { field: Union }>` narrows those fields to the codes the server documents.
 */
export type Coded<T, Codes extends { [K in keyof T]?: unknown }> = Omit<T, keyof Codes> & Codes;

type NullableKeys<T> = { [K in keyof T]-?: null extends T[K] ? K : never }[keyof T];

/**
 * Request records list every field as required, nullable ones included; the API reads a missing field as null.
 * `RequestBody<T>` makes the nullable fields optional so a body sends only what the user set.
 */
export type RequestBody<T> = Omit<T, NullableKeys<T>> & Partial<Pick<T, NullableKeys<T>>>;
