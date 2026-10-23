// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.19; shapes match backend Customers.Application/Admin/AdminCustomerService.cs and
// Customers.Application/Addresses/AddressService.cs.

import type { Id, Instant, Version } from "./common";

export type CustomerStatus = "ACTIVE" | "DELETED";

export interface CustomerListItem {
  customerId: Id;
  fullName: string | null;
  phone: string | null;
  email: string | null;
  status: CustomerStatus;
  isBlocked: boolean;
}

export interface CustomerListQuery {
  q?: string;
  status?: string;
  /** In docs/api §4.19; not yet accepted by the backend list request (ignored). */
  cityId?: Id;
  page?: number;
  pageSize?: number;
}

export interface CustomerAddress {
  addressId: Id;
  label: string;
  recipientName: string;
  phone: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  locality: string;
  cityName: string;
  stateCode: string;
  pincode: string;
  latitude: number | null;
  longitude: number | null;
  isDefault: boolean;
  isServiceable: boolean | null;
  version: Version;
}

/**
 * `GET /admin/customers/{id}`. Order counts and lifetime value (docs/api §4.19) are not in the backend
 * response yet; they are optional here and shown only when present.
 */
export interface CustomerDetail {
  customerId: Id;
  userId: Id;
  fullName: string | null;
  phone: string | null;
  email: string | null;
  marketingOptIn: boolean;
  status: CustomerStatus;
  isBlocked: boolean;
  deletedAt: Instant | null;
  addresses: CustomerAddress[];
  version: Version;
  orderCount?: number | null;
  deliveredOrderCount?: number | null;
  lifetimeValuePaise?: number | null;
}
