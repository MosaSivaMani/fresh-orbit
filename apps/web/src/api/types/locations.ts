// Serviceability, cities and delivery slots (docs/api §4.3, §4.5), generated from the backend OpenAPI document
// (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";

export type ServiceabilityResponse = Schema<"ServiceabilityResponse">;

export type PublicCity = Schema<"PublicCityResponse">;

/** The grievance officer the business profile names (D374): name and e-mail always, the rest when entered. */
export interface GrievanceOfficer {
  name: string;
  designation?: string | null;
  email: string;
  phone?: string | null;
  /** How soon a complaint is answered, in the owner's words. */
  responseTime?: string | null;
}

/**
 * The store's public contact details (`GET /business-profile`): support phone, email and hours, and the grievance
 * officer (null until entered). `grievanceOfficer` is declared here until the API client is regenerated (D374).
 */
export type PublicBusinessProfile = Schema<"PublicBusinessProfileResponse"> & {
  grievanceOfficer?: GrievanceOfficer | null;
};

export type SlotStatus = Schema<"SlotDisplayStatus">;

export type DeliverySlot = Schema<"CustomerSlotResponse">;

export type DeliverySlotsResponse = Schema<"CustomerSlotsResponse">;
