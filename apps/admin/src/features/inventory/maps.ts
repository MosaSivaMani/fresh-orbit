import { formatQuantity } from "@fresh/core";
import { type SelectOption, type StatusMap } from "@fresh/ui";
import { type StockQuantity } from "../../api/types/inventory";

/** Stock quantities arrive in the stock item's unit (grams or pieces); display only. */
export function qty(quantity: StockQuantity | null | undefined): string {
  return quantity ? formatQuantity(quantity) : "—";
}

export const RESERVATION_STATUS: StatusMap = {
  ACTIVE: { label: "Active", tone: "info" },
  CONSUMED: { label: "Consumed", tone: "success" },
  RELEASED: { label: "Released", tone: "neutral" },
  EXPIRED: { label: "Expired", tone: "warning" },
};

export const MOVEMENT_KIND: StatusMap = {
  RECEIPT: { label: "Receipt", tone: "success" },
  SALE: { label: "Sale", tone: "info" },
  WASTE: { label: "Waste", tone: "danger" },
  ADJUSTMENT_IN: { label: "Adjustment in", tone: "accent" },
  ADJUSTMENT_OUT: { label: "Adjustment out", tone: "warning" },
  RETURN: { label: "Return", tone: "accent" },
  COUNT_CORRECTION: { label: "Count correction", tone: "neutral" },
};

export const UNIT_LABEL: Readonly<Record<string, string>> = { GRAM: "grams", PIECE: "pieces" };

export const WASTE_REASON_OPTIONS: SelectOption[] = [
  { value: "SPOILED", label: "Spoiled" },
  { value: "TEMPERATURE_BREACH", label: "Temperature breach" },
  { value: "DAMAGED", label: "Damaged" },
  { value: "EXPIRED", label: "Expired" },
  { value: "QC_REJECTED", label: "Rejected at quality check" },
];

export const ADJUSTMENT_REASON_OPTIONS: SelectOption[] = [
  { value: "DATA_ENTRY_ERROR", label: "Data entry error" },
  { value: "FOUND", label: "Found" },
  { value: "LOST", label: "Lost" },
  { value: "THEFT", label: "Theft" },
  { value: "TRANSFER", label: "Transfer" },
  { value: "OTHER", label: "Other (notes required)" },
];

export const ADJUSTMENT_KIND_OPTIONS: SelectOption[] = [
  { value: "ADJUSTMENT_IN", label: "Add stock (adjustment in)" },
  { value: "ADJUSTMENT_OUT", label: "Remove stock (adjustment out)" },
  { value: "RETURN", label: "Return to a batch" },
];

export const STORAGE_UNIT_OPTIONS: SelectOption[] = [
  { value: "CHILLER", label: "Chiller (−1 to 4 °C)" },
  { value: "FREEZER", label: "Freezer (−18 °C or colder)" },
  { value: "RECEIPT", label: "Receipt check (4 °C or colder)" },
];
