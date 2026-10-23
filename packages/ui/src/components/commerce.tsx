import { formatRupees, formatWeight, formatWeightRange } from "@fresh/core";
import { Minus, Plus } from "lucide-react";
import { type ReactNode } from "react";
import { cn, focusRing } from "../lib/cn";
import { Spinner } from "./spinner";

// ---------- PriceTag ----------
const priceSizes = {
  sm: { price: "text-sm font-semibold", mrp: "text-xs" },
  md: { price: "text-lg font-semibold", mrp: "text-sm" },
  lg: { price: "text-2xl font-bold", mrp: "text-base" },
} as const;

export interface PriceTagProps {
  /** Server-computed price in paise. */
  paise: number | bigint;
  /** Server-provided list price in paise; shown struck through when higher than `paise`. */
  mrpPaise?: number | bigint | null;
  /** Unit suffix, e.g. "/ kg" or "per pack". */
  per?: string;
  /** e.g. "Approx." for weight-based items priced on final weight. */
  prefix?: string;
  /** Server-provided savings label, e.g. "12% off". This component never computes discounts. */
  savingsLabel?: string;
  size?: keyof typeof priceSizes;
  className?: string;
}

export function PriceTag({
  paise,
  mrpPaise,
  per,
  prefix,
  savingsLabel,
  size = "md",
  className,
}: PriceTagProps) {
  const s = priceSizes[size];
  const showMrp = mrpPaise != null && BigInt(mrpPaise) > BigInt(paise);
  return (
    <span className={cn("inline-flex flex-wrap items-baseline gap-x-2 gap-y-0.5", className)}>
      {prefix && <span className="text-text-muted text-xs">{prefix}</span>}
      <span className={cn("text-text tabular-nums", s.price)}>
        <span className="sr-only">Price </span>
        {formatRupees(paise)}
        {per && <span className="text-text-muted ml-0.5 text-sm font-normal">{per}</span>}
      </span>
      {showMrp && (
        <s className={cn("text-text-muted tabular-nums", s.mrp)}>
          <span className="sr-only">Original price </span>
          {formatRupees(mrpPaise)}
        </s>
      )}
      {savingsLabel && (
        <span className="text-success-text text-xs font-semibold">{savingsLabel}</span>
      )}
    </span>
  );
}

// ---------- WeightLabel ----------
export interface WeightLabelProps {
  /** Weight in grams (gross / selected). */
  grams?: number;
  /** Server-provided net range after cleaning, in grams. */
  netMinGrams?: number;
  netMaxGrams?: number;
  /** Label before the value, e.g. "Gross" or "Net wt." */
  prefix?: string;
  className?: string;
}

export function WeightLabel({
  grams,
  netMinGrams,
  netMaxGrams,
  prefix,
  className,
}: WeightLabelProps) {
  const hasNet = netMinGrams != null && netMaxGrams != null;
  return (
    <span
      className={cn(
        "text-text-muted inline-flex flex-wrap items-center gap-x-2 text-sm tabular-nums",
        className,
      )}
    >
      {grams != null && (
        <span>
          {prefix && `${prefix} `}
          {formatWeight(grams)}
        </span>
      )}
      {hasNet && (
        <span>
          {grams != null && <span aria-hidden="true">· </span>}
          Net {formatWeightRange(netMinGrams, netMaxGrams)}
        </span>
      )}
    </span>
  );
}

// ---------- QuantityStepper ----------
export interface QuantityStepperProps {
  value: number;
  onChange: (value: number) => void;
  /** Allowed bounds and step come from the server (variant min / max / step). */
  min?: number;
  max?: number;
  step?: number;
  /** Text for the current value, e.g. `(v) => formatQuantity({ value: v, unit: "GRAM" })`. */
  formatValue?: (value: number) => ReactNode;
  /** What is being counted; used in button labels ("Increase quantity of Seer fish"). */
  label: string;
  disabled?: boolean;
  /** Shows a spinner while the server confirms the change. */
  pending?: boolean;
  size?: "sm" | "md";
  className?: string;
}

/** − value + control. Only changes the requested amount; totals are re-read from the server. */
export function QuantityStepper({
  value,
  onChange,
  min = 0,
  max = Number.POSITIVE_INFINITY,
  step = 1,
  formatValue = (v) => String(v),
  label,
  disabled = false,
  pending = false,
  size = "md",
  className,
}: QuantityStepperProps) {
  const canDecrease = !disabled && !pending && value - step >= min;
  const canIncrease = !disabled && !pending && value + step <= max;
  const buttonClass = cn(
    "text-accent-text hover:bg-accent-subtle inline-flex items-center justify-center rounded-md disabled:cursor-not-allowed disabled:opacity-40",
    size === "sm" ? "size-8" : "size-10",
    focusRing,
  );
  return (
    <div
      role="group"
      aria-label={`Quantity of ${label}`}
      aria-busy={pending || undefined}
      className={cn(
        "border-border-strong bg-surface inline-flex items-center rounded-lg border p-0.5",
        className,
      )}
    >
      <button
        type="button"
        className={buttonClass}
        onClick={() => onChange(value - step)}
        disabled={!canDecrease}
        aria-label={`Decrease quantity of ${label}`}
      >
        <Minus aria-hidden="true" className="size-4" />
      </button>
      <output
        aria-live="polite"
        className={cn(
          "text-text flex min-w-16 items-center justify-center px-2 font-semibold tabular-nums",
          size === "sm" ? "text-sm" : "text-base",
        )}
      >
        {pending ? <Spinner size="sm" label="Updating quantity" /> : formatValue(value)}
      </output>
      <button
        type="button"
        className={buttonClass}
        onClick={() => onChange(value + step)}
        disabled={!canIncrease}
        aria-label={`Increase quantity of ${label}`}
      >
        <Plus aria-hidden="true" className="size-4" />
      </button>
    </div>
  );
}
