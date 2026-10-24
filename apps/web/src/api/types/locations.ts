// Serviceability, cities and delivery slots (docs/api §4.3, §4.5), generated from the backend OpenAPI document
// (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";

export type ServiceabilityResponse = Schema<"ServiceabilityResponse">;

export type PublicCity = Schema<"PublicCityResponse">;

export type SlotStatus = Schema<"SlotDisplayStatus">;

export type DeliverySlot = Schema<"CustomerSlotResponse">;

export type DeliverySlotsResponse = Schema<"CustomerSlotsResponse">;
