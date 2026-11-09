// Business profile, order policies, number series (docs/api §4.21), generated from the backend OpenAPI document.

import type { Coded, Schema } from "./common";

/**
 * Grievance officer fields of the business profile (D374). Declared here until the API client is regenerated from
 * the OpenAPI document of this change; the intersection stays correct afterwards.
 */
interface GrievanceOfficerFields {
  grievanceOfficerName?: string | null;
  grievanceOfficerDesignation?: string | null;
  grievanceOfficerEmail?: string | null;
  grievanceOfficerPhone?: string | null;
  grievanceResponseTime?: string | null;
}

export type BusinessProfile = Schema<"BusinessProfileResponse"> & GrievanceOfficerFields;

type SaveProfileSchema = Schema<"SaveBusinessProfileRequest">;

export type SaveBusinessProfileRequest = SaveProfileSchema & GrievanceOfficerFields;

export type CancelAllowedUntilStatus = "PENDING_PAYMENT" | "CONFIRMED" | "PREPARING";
export type PolicyPaymentMethod = Schema<"PaymentsPaymentMethod">;

/** `cityId` is null for the default policy. */
export type OrderPolicy = Coded<
  Schema<"OrderPolicyResponse">,
  {
    cancelAllowedUntilStatus: CancelAllowedUntilStatus;
    enabledPaymentMethods: PolicyPaymentMethod[];
  }
>;

export type SaveOrderPolicyRequest = Coded<
  Schema<"SaveOrderPolicyRequest">,
  {
    cancelAllowedUntilStatus: CancelAllowedUntilStatus;
    enabledPaymentMethods: PolicyPaymentMethod[];
  }
>;

export interface OrderPolicyList {
  items: OrderPolicy[];
}

/** `nextNumberPreview` is server-rendered; `isInUse` locks prefix and next value (`NUMBER_SERIES_IN_USE`). */
export type NumberSeries = Schema<"NumberSeriesResponse">;

export type SaveNumberSeriesRequest = Schema<"SaveNumberSeriesRequest">;

export type NumberSeriesList = Schema<"NumberSeriesListResponse">;
