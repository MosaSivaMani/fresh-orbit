// Customers (docs/api §4.19), generated from the backend OpenAPI document.

import type { Coded, Id, Schema } from "./common";

export type CustomerStatus = "ACTIVE" | "DELETED";

export type CustomerListItem = Coded<Schema<"CustomerListItem">, { status: CustomerStatus }>;

export interface CustomerListQuery {
  q?: string;
  status?: string;
  /** In docs/api §4.19; the backend list does not accept it yet (gap G6). */
  cityId?: Id;
  page?: number;
  pageSize?: number;
}

export type CustomerAddress = Schema<"AddressResponse">;

/**
 * `GET /admin/customers/{id}`. Order counts and lifetime value (docs/api §4.19) are not in the response; the screen
 * does not show them (gap G6).
 */
export type CustomerDetail = Coded<Schema<"CustomerDetailResponse">, { status: CustomerStatus }>;
