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
  /**
   * Bold blocks look (D88): `outline` = teal border on paper (cart lines, product page), `solid` = teal fill
   * (the ADD button turned stepper on product cards). Default outline.
   */
  variant?: "outline" | "solid";
  /**
   * Going below `min` proposes 0 ("Remove X from cart") instead of being disabled, so a card can fall back to
   * its ADD button. The server still decides what is in the cart.
   */
  allowRemove?: boolean;
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
  variant = "outline",
  allowRemove = false,
  className,
}: QuantityStepperProps) {
  const busy = disabled || pending;
  const below = value - step < min;
  const removes = allowRemove && below && value > 0;
  const canDecrease = !busy && (!below || removes);
  const canIncrease = !busy && value + step <= max;
  const solid = variant === "solid";
  const buttonClass = cn(
    "inline-flex cursor-pointer items-center justify-center rounded-lg disabled:cursor-not-allowed disabled:opacity-40 [&_svg]:stroke-[2.75]",
    solid ? "hover:bg-accent-hover" : "text-accent-text hover:bg-teal-soft",
    size === "sm" ? "size-8 [&_svg]:size-3.5" : "size-10 [&_svg]:size-4",
    focusRing,
  );
  return (
    <div
      role="group"
      aria-label={`Quantity of ${label}`}
      aria-busy={pending || undefined}
      data-variant={variant}
      className={cn(
        "inline-flex items-center rounded-[10px] p-0.5",
        solid
          ? "bg-accent text-on-accent shadow-soft-sm"
          : "border-accent bg-surface-paper text-accent-text border-[1.5px]",
        className,
      )}
    >
      <button
        type="button"
        className={buttonClass}
        onClick={() => onChange(removes ? 0 : value - step)}
        disabled={!canDecrease}
        aria-label={removes ? `Remove ${label} from cart` : `Decrease quantity of ${label}`}
      >
        <Minus aria-hidden="true" />
      </button>
      <output
        aria-live="polite"
        className={cn(
          "flex items-center justify-center px-1.5 font-extrabold tabular-nums",
          size === "sm" ? "min-w-8 text-sm" : "min-w-14 text-base",
          !solid && "text-accent-text",
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
        <Plus aria-hidden="true" />
      </button>
    </div>
  );
}
