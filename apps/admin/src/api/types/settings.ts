// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.21 (business profile, order policies, number series); shapes match backend
// Settings.Application/{BusinessProfiles,OrderPolicies,Numbering}/*Service.cs.

import type { Id, Instant, Version } from "./common";

export interface BusinessProfile {
  legalName: string;
  tradeName: string;
  gstin: string | null;
  fssaiLicenseNo: string | null;
  address: string;
  /** E.164. */
  supportPhone: string;
  supportEmail: string;
  /** IANA, e.g. `Asia/Kolkata`. */
  timeZone: string;
  version: Version;
}

export type SaveBusinessProfileRequest = Omit<BusinessProfile, "version">;

export type CancelAllowedUntilStatus = "PENDING_PAYMENT" | "CONFIRMED" | "PREPARING";
export type PolicyPaymentMethod = "COD" | "UPI_ON_DELIVERY" | "ONLINE";

export interface OrderPolicy {
  id: Id;
  /** null for the default policy. */
  cityId: Id | null;
  isDefault: boolean;
  reservationMinutes: number;
  maxOverweightBp: number;
  cancelAllowedUntilStatus: CancelAllowedUntilStatus;
  codMaxOrderPaise: number;
  enabledPaymentMethods: PolicyPaymentMethod[];
  substitutionResponseMinutes: number;
  complaintWindowHours: number;
  maxDeliveryAttempts: number;
  version: Version;
}

export type SaveOrderPolicyRequest = Omit<OrderPolicy, "id" | "cityId" | "isDefault" | "version">;

export interface OrderPolicyList {
  items: OrderPolicy[];
}

export interface NumberSeries {
  code: string;
  period: string;
  prefix: string;
  padWidth: number;
  nextValue: number;
  /** Server-rendered next number, e.g. `INV/26-27/000001`. */
  nextNumberPreview: string;
  /** Numbers already issued: prefix and next value are locked (`NUMBER_SERIES_IN_USE`). */
  isInUse: boolean;
  lastIssuedAt: Instant | null;
  version: Version;
}

export interface SaveNumberSeriesRequest {
  prefix: string;
  padWidth: number;
  nextValue: number;
}

export interface NumberSeriesList {
  items: NumberSeries[];
}
