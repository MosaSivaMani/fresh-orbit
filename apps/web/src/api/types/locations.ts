// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §4.3 (serviceability, cities) and §4.5 (delivery slots).

export interface ServiceabilityResponse {
  pincode: string;
  serviceable: boolean;
  /** `PINCODE_NOT_SERVICEABLE` (or `CITY_NOT_ACTIVE`) when not served. */
  reasonCode: string | null;
  city: { id: string; code: string; name: string } | null;
  fulfillmentLocation: { id: string; code: string; name: string; hasColdStorage: boolean } | null;
  serviceArea: {
    id: string;
    name: string;
    deliveryFeePaise: number;
    freeDeliveryAbovePaise: number | null;
    minOrderPaise: number;
  } | null;
  nextSlot: { id: string; startAt: string; endAt: string } | null;
}

export interface PublicCity {
  id: string;
  code: string;
  name: string;
  stateCode?: string;
}

export type SlotStatus = "AVAILABLE" | "FULL" | "CUTOFF_PASSED" | "CLOSED";

export interface DeliverySlot {
  id: string;
  /** Location-local date `YYYY-MM-DD`. */
  slotDate: string;
  startAt: string;
  endAt: string;
  cutoffAt: string;
  status: SlotStatus;
}

export interface DeliverySlotsResponse {
  fulfillmentLocationId: string;
  items: DeliverySlot[];
}
