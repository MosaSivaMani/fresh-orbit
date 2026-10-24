// Customers (docs/api §4.19), generated from the backend OpenAPI document.

import type { Coded, Id, Schema } from "./common";
import type { OrderStatus, PaymentMethod } from "./orders";

export type CustomerStatus = "ACTIVE" | "DELETED";

export type CustomerListItem = Coded<Schema<"CustomerListItem">, { status: CustomerStatus }>;

export interface CustomerListQuery {
  q?: string;
  status?: string;
  /** Customers with an address in this city (G6). */
  cityId?: Id;
  page?: number;
  pageSize?: number;
}

export type CustomerAddress = Schema<"AddressResponse">;

/** `GET /admin/customers/{id}` with the customer's order counts (G6). */
export type CustomerDetail = Coded<Schema<"CustomerDetailResponse">, { status: CustomerStatus }>;

/** A row of `GET /admin/customers/{id}/orders` (G6), newest first. */
export type CustomerOrder = Coded<
  Schema<"CustomerOrderListItem">,
  { status: OrderStatus; paymentMethod: PaymentMethod }
>;
