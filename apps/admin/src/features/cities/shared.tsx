import { isApiError } from "@fresh/core";
import { type DayOfWeekName, type ReadinessCode } from "../../api/types/locations";

// Labels and small building blocks shared by the cities, locations and service-area screens.

export { FormAlert, reportWriteError } from "../delivery/shared";

/** Readiness items in the order a launch team works through them, with where each is fixed. */
export const READINESS_ITEMS: readonly {
  code: ReadinessCode;
  label: string;
  fix: { label: string; to: string | ((cityId: string) => string) };
}[] = [
  {
    code: "FULFILLMENT_LOCATION_MISSING",
    label: "At least one active fulfillment location",
    fix: { label: "Add a location", to: (cityId) => `/cities/${cityId}#locations` },
  },
  {
    code: "SERVICE_AREA_MISSING",
    label: "Every location serves an active service area",
    fix: { label: "Add a service area", to: (cityId) => `/cities/${cityId}#service-areas` },
  },
  {
    code: "PINCODE_MISSING",
    label: "Every service area has pincodes",
    fix: { label: "Add pincodes", to: (cityId) => `/cities/${cityId}#service-areas` },
  },
  {
    code: "SLOT_TEMPLATE_MISSING",
    label: "Every location has active delivery slot templates",
    fix: { label: "Open slot templates", to: "/delivery/templates" },
  },
  {
    code: "PRICE_MISSING",
    label: "Products are priced at every location",
    fix: { label: "Open pricing", to: "/pricing" },
  },
  {
    code: "STOCK_ITEM_MISSING",
    label: "Stock is set up at every location",
    fix: { label: "Open inventory", to: "/inventory" },
  },
];

export const WEEK: readonly { day: DayOfWeekName; label: string }[] = [
  { day: "MONDAY", label: "Monday" },
  { day: "TUESDAY", label: "Tuesday" },
  { day: "WEDNESDAY", label: "Wednesday" },
  { day: "THURSDAY", label: "Thursday" },
  { day: "FRIDAY", label: "Friday" },
  { day: "SATURDAY", label: "Saturday" },
  { day: "SUNDAY", label: "Sunday" },
];

/** Problem Details extension member as readable sentences (`missing`, `blocking`), whatever its item shape. */
export function problemItems(error: unknown, member: string): string[] {
  if (!isApiError(error)) return [];
  const value = (error.problem as Record<string, unknown>)[member];
  if (!Array.isArray(value)) return [];
  return value.map((item: unknown) => {
    if (typeof item === "string") return item;
    if (item && typeof item === "object") {
      const o = item as Record<string, unknown>;
      if (typeof o.message === "string") return o.message;
      if (typeof o.code === "string") return o.code;
    }
    return JSON.stringify(item);
  });
}

export const LOCATION_STATES = [
  { value: "DRAFT", label: "Draft" },
  { value: "ACTIVE", label: "Active" },
  { value: "PAUSED", label: "Paused" },
];

/** Optional decimal (latitude / longitude) typed by staff → number or null. */
export function optionalNumber(text: string): number | null {
  const t = text.trim();
  return t === "" ? null : Number(t);
}

export function emptyToNull(text: string): string | null {
  const t = text.trim();
  return t === "" ? null : t;
}
