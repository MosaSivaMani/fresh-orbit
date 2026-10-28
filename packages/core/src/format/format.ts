// Display formatting only. No business calculations: prices, totals, weights and discounts arrive from the
// server already computed; these functions turn them into text.

export const LOCALE = "en-IN";
export const IST_TIME_ZONE = "Asia/Kolkata";

export type QuantityUnit = "GRAM" | "PIECE" | "PACK";
export interface Quantity {
  value: number;
  unit: QuantityUnit | (string & {});
}

const rupeeFormatters = new Map<string, Intl.NumberFormat>();
function rupeeFormatter(fractionDigits: number): Intl.NumberFormat {
  const key = String(fractionDigits);
  let f = rupeeFormatters.get(key);
  if (!f) {
    f = new Intl.NumberFormat(LOCALE, {
      style: "currency",
      currency: "INR",
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    });
    rupeeFormatters.set(key, f);
  }
  return f;
}

export interface RupeeOptions {
  /** `auto` (default) shows paise only when non-zero; `always` shows two decimals; `never` hides them. */
  paise?: "auto" | "always" | "never";
}

/** 124950 -> "₹1,249.50"; 1250000 -> "₹12,500"; Indian digit grouping (₹1,24,999). */
export function formatRupees(paise: number | bigint, options: RupeeOptions = {}): string {
  const total = typeof paise === "bigint" ? paise : BigInt(Math.trunc(paise));
  const negative = total < 0n;
  const abs = negative ? -total : total;
  const rupees = abs / 100n;
  const rest = abs % 100n;
  const mode = options.paise ?? "auto";
  const showFraction = mode === "always" || (mode === "auto" && rest !== 0n);
  // Intl accepts decimal strings without float rounding.
  const amount = `${negative ? "-" : ""}${rupees}.${rest.toString().padStart(2, "0")}`;
  return rupeeFormatter(showFraction ? 2 : 0).format(amount as unknown as number);
}

const trimNumber = (n: number, maxDigits: number): string =>
  new Intl.NumberFormat(LOCALE, { maximumFractionDigits: maxDigits }).format(n);

/** 500 -> "500 g"; 1000 -> "1 kg"; 1250 -> "1.25 kg". `unit` forces g or kg. */
export function formatWeight(grams: number, options: { unit?: "auto" | "g" | "kg" } = {}): string {
  const unit = options.unit ?? "auto";
  if (unit === "g" || (unit === "auto" && Math.abs(grams) < 1000))
    return `${trimNumber(grams, 0)} g`;
  return `${trimNumber(grams / 1000, 3)} kg`;
}

/** A weight range, e.g. net weight after cleaning: (450, 550) -> "450–550 g"; (900, 1100) -> "0.9–1.1 kg". */
export function formatWeightRange(minGrams: number, maxGrams: number): string {
  if (minGrams === maxGrams) return formatWeight(minGrams);
  if (maxGrams < 1000) return `${trimNumber(minGrams, 0)}–${trimNumber(maxGrams, 0)} g`;
  return `${trimNumber(minGrams / 1000, 3)}–${trimNumber(maxGrams / 1000, 3)} kg`;
}

/** `{value:500, unit:"GRAM"}` -> "500 g"; `{value:2, unit:"PIECE"}` -> "2 pieces"; `{value:1, unit:"PACK"}` -> "1 pack". */
export function formatQuantity(quantity: Quantity): string {
  const { value, unit } = quantity;
  switch (unit) {
    case "GRAM":
      return formatWeight(value);
    case "PIECE":
      return `${trimNumber(value, 0)} ${value === 1 ? "piece" : "pieces"}`;
    case "PACK":
      return `${trimNumber(value, 0)} ${value === 1 ? "pack" : "packs"}`;
    default:
      return `${trimNumber(value, 3)} ${unit.toLowerCase().replace(/_/g, " ")}`;
  }
}

/** Basis points as a percentage: 500 -> "5%"; 1250 -> "12.5%". */
export function formatPercentFromBp(bp: number): string {
  return `${trimNumber(bp / 100, 2)}%`;
}

type DateInput = string | number | Date;
const toDate = (value: DateInput): Date => (value instanceof Date ? value : new Date(value));

const dateFormatters = new Map<string, Intl.DateTimeFormat>();
function dtf(key: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  let f = dateFormatters.get(key);
  if (!f) {
    f = new Intl.DateTimeFormat(LOCALE, options);
    dateFormatters.set(key, f);
  }
  return f;
}

/**
 * The zone a time is shown in: the server's IANA `timeZone` (the store's, W10) when given, else India Standard Time.
 * A missing or unknown zone falls back to IST instead of throwing while a page renders.
 */
function zoneOf(timeZone: string | null | undefined): string {
  if (!timeZone) return IST_TIME_ZONE;
  try {
    new Intl.DateTimeFormat(LOCALE, { timeZone });
    return timeZone;
  } catch {
    return IST_TIME_ZONE;
  }
}

/** UTC instant -> date in the store's zone (IST by default): "23 Oct 2026". */
export function formatDate(value: DateInput, timeZone?: string | null): string {
  const zone = zoneOf(timeZone);
  return dtf(`date:${zone}`, {
    timeZone: zone,
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(toDate(value));
}

/** UTC instant -> time in the store's zone (IST by default): "11:00 am". */
export function formatTime(value: DateInput, timeZone?: string | null): string {
  const zone = zoneOf(timeZone);
  return dtf(`time:${zone}`, {
    timeZone: zone,
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  }).format(toDate(value));
}

/** UTC instant -> date and time in the store's zone (IST by default): "23 Oct 2026, 11:00 am". */
export function formatDateTime(value: DateInput, timeZone?: string | null): string {
  return `${formatDate(value, timeZone)}, ${formatTime(value, timeZone)}`;
}

/** UTC instant -> short weekday and date in the store's zone (IST by default): "Fri, 23 Oct". */
export function formatDayShort(value: DateInput, timeZone?: string | null): string {
  const zone = zoneOf(timeZone);
  return dtf(`day:${zone}`, {
    timeZone: zone,
    weekday: "short",
    day: "numeric",
    month: "short",
  }).format(toDate(value));
}

/** Calendar date `YYYY-MM-DD` (no time zone shift): "2026-10-23" -> "23 Oct 2026". */
export function formatCalendarDate(isoDate: string): string {
  const [y, m, d] = isoDate.split("-").map(Number);
  if (!y || !m || !d) return isoDate;
  return dtf("cal", { timeZone: "UTC", day: "numeric", month: "short", year: "numeric" }).format(
    new Date(Date.UTC(y, m - 1, d)),
  );
}

/** Time of day `HH:mm` already in the location's zone (slots, opening hours): "07:00" -> "7:00 am". */
export function formatTimeOfDay(hhmm: string): string {
  const [h, m] = hhmm.split(":").map(Number);
  if (h === undefined || m === undefined || Number.isNaN(h) || Number.isNaN(m)) return hhmm;
  return dtf("tod", { timeZone: "UTC", hour: "numeric", minute: "2-digit", hour12: true }).format(
    new Date(Date.UTC(2000, 0, 1, h, m)),
  );
}

/** Delivery slot window: ("07:00", "09:00") -> "7:00 am – 9:00 am". */
export function formatTimeRange(startHhmm: string, endHhmm: string): string {
  return `${formatTimeOfDay(startHhmm)} – ${formatTimeOfDay(endHhmm)}`;
}

/** UPPER_SNAKE code -> sentence label when no explicit label is known: "OUT_FOR_DELIVERY" -> "Out for delivery". */
export function humanizeCode(code: string): string {
  const words = code.toLowerCase().split("_").filter(Boolean).join(" ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}
