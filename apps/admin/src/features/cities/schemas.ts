import { z } from "zod";
import { parseRupeesToPaise } from "../../lib/units";

// Form fields shared by the create and edit forms of locations and service areas (rupees typed, paise sent).

const decimal = (label: string) =>
  z
    .string()
    .trim()
    .refine(
      (v) => v === "" || Number.isFinite(Number(v)),
      `Enter the ${label} as a decimal number.`,
    );

export const rupees = (message: string) =>
  z
    .string()
    .trim()
    .refine((v) => parseRupeesToPaise(v) !== null, message);

export const optionalRupees = z
  .string()
  .trim()
  .refine(
    (v) => v === "" || parseRupeesToPaise(v) !== null,
    "Enter an amount in rupees, e.g. 999, or leave empty.",
  );

export const locationFields = {
  name: z.string().trim().min(1, "Enter the location name.").max(120),
  addressLine: z.string().trim().max(300),
  pincode: z
    .string()
    .trim()
    .refine((v) => v === "" || /^[1-9]\d{5}$/.test(v), "Enter a 6-digit pincode."),
  phone: z
    .string()
    .trim()
    .refine(
      (v) => v === "" || /^\+[1-9]\d{7,14}$/.test(v),
      "Enter the phone with country code, e.g. +914012345678.",
    ),
  latitude: decimal("latitude"),
  longitude: decimal("longitude"),
  hasColdStorage: z.boolean(),
  fssaiLicenseNo: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\d{14}$/.test(v), "Enter the 14-digit FSSAI licence number."),
};

export const LOCATION_FIELD_NAMES = [
  "code",
  "name",
  "addressLine",
  "pincode",
  "phone",
  "latitude",
  "longitude",
  "hasColdStorage",
  "fssaiLicenseNo",
  "status",
];
