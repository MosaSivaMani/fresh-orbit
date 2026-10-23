// Input parsing for staff-entered amounts. These convert what a person types into the API's units (paise,
// grams); they never compute business values (CLAUDE.md rule 4). Display uses @fresh/core formatters.

const RUPEES = /^\s*(\d{1,10})(?:\.(\d{1,2}))?\s*$/;

/** "1249.5" → 124950 paise. Returns null for anything that is not a plain non-negative rupee amount. */
export function parseRupeesToPaise(text: string): number | null {
  const match = RUPEES.exec(text.replace(/,/g, ""));
  if (!match) return null;
  const rupees = Number(match[1]);
  const paise = Number((match[2] ?? "").padEnd(2, "0"));
  return rupees * 100 + paise;
}

/** 124950 → "1249.50" for an input's initial value. */
export function paiseToRupeesInput(paise: number | null | undefined): string {
  if (paise == null) return "";
  const sign = paise < 0 ? "-" : "";
  const abs = Math.abs(paise);
  return `${sign}${Math.floor(abs / 100)}.${String(abs % 100).padStart(2, "0")}`;
}

/** Whole grams from an input ("540" → 540). */
export function parseWholeNumber(text: string): number | null {
  const trimmed = text.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  return Number(trimmed);
}

/** "5" → 500 basis points for a percentage typed by staff (up to two decimals). */
export function parsePercentToBp(text: string): number | null {
  const match = /^\s*(\d{1,3})(?:\.(\d{1,2}))?\s*$/.exec(text);
  if (!match) return null;
  return Number(match[1]) * 100 + Number((match[2] ?? "").padEnd(2, "0"));
}

export function bpToPercentInput(bp: number | null | undefined): string {
  if (bp == null) return "";
  const whole = Math.floor(bp / 100);
  const frac = bp % 100;
  return frac === 0 ? String(whole) : `${whole}.${String(frac).padStart(2, "0")}`;
}

/** Today's date (YYYY-MM-DD) in India, for date-input defaults. */
export function todayInIndia(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Kolkata" }).format(new Date());
}

/** `datetime-local` value (IST wall clock) → ISO UTC instant. */
export function istLocalToUtcIso(local: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(local)) return null;
  // India has no daylight saving: IST is always UTC+05:30.
  return new Date(`${local}:00+05:30`).toISOString();
}

/** ISO UTC instant → `datetime-local` value in IST. */
export function utcIsoToIstLocal(iso: string | null | undefined): string {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  const shifted = new Date(date.getTime() + 330 * 60_000);
  return shifted.toISOString().slice(0, 16);
}
