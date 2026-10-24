// Cities, fulfillment locations, service areas (docs/api §4.18), generated from the backend OpenAPI document.

import type { Coded, Schema } from "./common";

export type CityStatus = Schema<"CityStatus">;
export type LocationStatus = Schema<"LocationStatus">;
/** .NET `DayOfWeek` written as its code. */
export type DayOfWeekName = Schema<"DayOfWeek">;

export type City = Schema<"CityResponse">;

/** `GET /admin/cities/lookup` item, readable by any staff member (G2). */
export type CityLookup = Coded<Schema<"CityLookupItem">, { status: CityStatus }>;

/** `GET /admin/me/locations` item: one of the caller's own locations (G1). */
export type StaffLocation = Coded<Schema<"StaffLocationItem">, { status: LocationStatus }>;

export type ReadinessCode =
  | "FULFILLMENT_LOCATION_MISSING"
  | "SERVICE_AREA_MISSING"
  | "PINCODE_MISSING"
  | "SLOT_TEMPLATE_MISSING"
  | "PRICE_MISSING"
  | "STOCK_ITEM_MISSING"
  | (string & {});

export type MissingItem = Coded<Schema<"MissingItemResponse">, { code: ReadinessCode }>;

export type Readiness = Coded<Schema<"ReadinessResponse">, { missing: MissingItem[] }>;

export type CityDetail = Coded<Schema<"CityDetailResponse">, { readiness: Readiness }>;

export type CreateCityRequest = Schema<"CreateCityRequest">;

export type UpdateCityRequest = Schema<"UpdateCityRequest">;

export type OperatingHours = Schema<"OperatingHoursResponse">;

export type FulfillmentLocation = Schema<"FulfillmentLocationResponse">;

export type CreateFulfillmentLocationRequest = Schema<"CreateFulfillmentLocationRequest">;

/** `status: null` keeps the current status. */
export type UpdateFulfillmentLocationRequest = Schema<"UpdateFulfillmentLocationRequest">;

export type OperatingHoursDay = Schema<"OperatingHoursDay">;

/** The whole week; a day not listed is closed. */
export type SetOperatingHoursRequest = Schema<"SetOperatingHoursRequest">;

export type Closure = Schema<"ClosureResponse">;

export type AddClosureRequest = Schema<"AddClosureRequest">;

export type ServiceArea = Schema<"ServiceAreaResponse">;

export type CreateServiceAreaRequest = Schema<"CreateServiceAreaRequest">;

export type UpdateServiceAreaRequest = Schema<"UpdateServiceAreaRequest">;

export type Pincode = Schema<"PincodeResponse">;

export type AddPincodesResponse = Schema<"AddPincodesResponse">;
