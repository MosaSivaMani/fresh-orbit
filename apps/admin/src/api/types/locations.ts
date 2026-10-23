// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.18; shapes match backend Locations.Application/*Contracts.cs.

import type { CalendarDate, Id, Instant, TimeOfDay, Version } from "./common";

export type CityStatus = "DRAFT" | "ACTIVE" | "PAUSED" | "RETIRED";
export type LocationStatus = "DRAFT" | "ACTIVE" | "PAUSED";
/** .NET `DayOfWeek` serialised as a string. */
export type DayOfWeekName =
  "SUNDAY" | "MONDAY" | "TUESDAY" | "WEDNESDAY" | "THURSDAY" | "FRIDAY" | "SATURDAY";

export interface City {
  id: Id;
  code: string;
  name: string;
  stateCode: string;
  timeZone: string;
  status: CityStatus;
  activatedAt: Instant | null;
  activatedBy: Id | null;
  version: Version;
}

export type ReadinessCode =
  | "FULFILLMENT_LOCATION_MISSING"
  | "SERVICE_AREA_MISSING"
  | "PINCODE_MISSING"
  | "SLOT_TEMPLATE_MISSING"
  | "PRICE_MISSING"
  | "STOCK_ITEM_MISSING"
  | (string & {});

export interface MissingItem {
  code: ReadinessCode;
  fulfillmentLocationId: Id | null;
  message: string;
}

export interface Readiness {
  cityId: Id;
  isReady: boolean;
  missing: MissingItem[];
}

export interface CityDetail extends City {
  readiness: Readiness;
}

export interface CreateCityRequest {
  code: string;
  name: string;
  stateCode: string;
  /** IANA, e.g. `Asia/Kolkata`. */
  timeZone: string;
}

export type UpdateCityRequest = Omit<CreateCityRequest, "code">;

export interface OperatingHours {
  dayOfWeek: DayOfWeekName;
  opensAt: TimeOfDay;
  closesAt: TimeOfDay;
}

export interface FulfillmentLocation {
  id: Id;
  cityId: Id;
  code: string;
  name: string;
  addressLine: string | null;
  pincode: string | null;
  phone: string | null;
  latitude: number | null;
  longitude: number | null;
  hasColdStorage: boolean;
  fssaiLicenseNo: string | null;
  status: LocationStatus;
  operatingHours: OperatingHours[];
  version: Version;
}

export interface CreateFulfillmentLocationRequest {
  cityId: Id;
  code: string;
  name: string;
  addressLine: string | null;
  pincode: string | null;
  phone: string | null;
  latitude: number | null;
  longitude: number | null;
  hasColdStorage: boolean;
  fssaiLicenseNo: string | null;
}

export interface UpdateFulfillmentLocationRequest extends Omit<
  CreateFulfillmentLocationRequest,
  "cityId" | "code"
> {
  /** null keeps the current status. */
  status: LocationStatus | null;
}

export interface OperatingHoursDay {
  dayOfWeek: DayOfWeekName;
  opensAt: TimeOfDay | null;
  closesAt: TimeOfDay | null;
}

export interface SetOperatingHoursRequest {
  /** The whole week; a day not listed is closed. */
  days: OperatingHoursDay[];
}

export interface Closure {
  id: Id;
  fulfillmentLocationId: Id;
  closedOn: CalendarDate;
  reason: string | null;
}

export interface AddClosureRequest {
  closedOn: CalendarDate;
  reason: string | null;
}

export interface ServiceArea {
  id: Id;
  cityId: Id;
  fulfillmentLocationId: Id;
  name: string;
  deliveryFeePaise: number;
  freeDeliveryAbovePaise: number | null;
  minOrderPaise: number;
  isActive: boolean;
  activePincodeCount: number;
  version: Version;
}

export interface CreateServiceAreaRequest {
  cityId: Id;
  fulfillmentLocationId: Id;
  name: string;
  deliveryFeePaise: number;
  freeDeliveryAbovePaise: number | null;
  minOrderPaise: number;
}

export interface UpdateServiceAreaRequest {
  name: string;
  deliveryFeePaise: number;
  freeDeliveryAbovePaise: number | null;
  minOrderPaise: number;
  isActive: boolean;
}

export interface Pincode {
  pincode: string;
  isActive: boolean;
  addedAt: Instant;
}

export interface AddPincodesResponse {
  serviceAreaId: Id;
  added: string[];
  activePincodeCount: number;
  version: Version;
}
