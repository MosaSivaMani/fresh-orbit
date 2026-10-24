// Food-app (D95) pieces for the website's cart, checkout, orders and account pages: a numbered step card, the
// bill box, the sticky action bar (Place order) and the order status steps.
//
// Phone and desktop widths use these same components. None of them compute business values (CLAUDE.md rule 4):
// every amount, label and time is passed in already formatted from what the server sent; the bill box never adds
// its rows up and the status steps only show which step the server's status reached.
import { type ComponentProps, type ReactNode, createElement, useId } from "react";
import { cn } from "../lib/cn";

type HeadingLevel = 2 | 3 | 4;
const heading = (level: HeadingLevel) => `h${level}` as const;

// ---------- StepCard ----------
export interface StepCardProps extends Omit<ComponentProps<"section">, "title"> {
  /** Step number shown in the round coral badge; leave out for a plain section card. */
  step?: number;
  title: ReactNode;
  /** Right-side content of the title row (an "Add" button, a change link). */
  action?: ReactNode;
  headingLevel?: HeadingLevel;
}

/** A white rounded card with a Baloo 2 title, optionally numbered (checkout steps, cart sections). */
export function StepCard({
  step,
  title,
  action,
  headingLevel = 2,
  className,
  children,
  ...props
}: StepCardProps) {
  const titleId = useId();
  return (
    <section
      aria-labelledby={titleId}
      className={cn(
        "border-border bg-surface-paper text-text grid gap-4 rounded-[22px] border p-4 shadow-[0_1px_2px_rgb(40_30_10/0.06),0_8px_22px_rgb(40_30_10/0.07)] sm:p-5",
        className,
      )}
      {...props}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        {createElement(
          heading(headingLevel),
          {
            id: titleId,
            className:
              "font-display flex items-center gap-2.5 text-xl leading-tight font-extrabold",
          },
          step != null && (
            <span
              aria-hidden="true"
              className="bg-block-coral text-on-block-coral grid size-8 shrink-0 place-items-center rounded-full font-sans text-sm font-extrabold"
            >
              {step}
            </span>
          ),
          step != null && <span className="sr-only">{`Step ${step}:`}</span>,
          step != null && " ",
          title,
        )}
        {action}
      </div>
      {children}
    </section>
  );
}

// ---------- BillBox ----------
export interface BillRow {
  /** Stable key; defaults to the position. */
  id?: string;
  label: ReactNode;
  /** The server's value, already formatted ("₹649.50", "Free"). Shown as given. */
  value: ReactNode;
  /** `saving` for a discount, `muted` for a note row. */
  tone?: "default" | "saving" | "muted";
}

export interface BillBoxProps extends Omit<ComponentProps<"section">, "title"> {
  /** Default "Bill details". */
  title?: ReactNode;
  rows: readonly BillRow[];
  /** The total exactly as the server sent it. Never the sum of `rows` worked out here. */
  total: { label: ReactNode; value: ReactNode };
  /** Notes under the total (weighed items, taxes included, free-delivery shortfall), server values only. */
  notes?: ReactNode;
  headingLevel?: HeadingLevel;
}

const billTone = {
  default: "",
  saving: "text-success-text",
  muted: "text-text-muted",
} as const;

/** The bill: label/value rows, a dashed rule and the total in the display face. */
export function BillBox({
  title = "Bill details",
  rows,
  total,
  notes,
  headingLevel = 2,
  className,
  children,
  ...props
}: BillBoxProps) {
  const titleId = useId();
  return (
    <section
      aria-labelledby={titleId}
      className={cn("bg-surface-sand text-text grid gap-3 rounded-[20px] p-4 sm:p-5", className)}
      {...props}
    >
      {createElement(
        heading(headingLevel),
        { id: titleId, className: "font-display text-lg leading-tight font-extrabold" },
        title,
      )}
      <dl className="grid gap-2 text-sm">
        {rows.map((row, i) => (
          <div
            key={row.id ?? i}
            className={cn(
              "flex items-baseline justify-between gap-3",
              billTone[row.tone ?? "default"],
            )}
          >
            <dt className="min-w-0">{row.label}</dt>
            <dd className="shrink-0 text-right tabular-nums">{row.value}</dd>
          </div>
        ))}
        <div
          data-bill-total=""
          className="border-border-strong mt-1 flex items-baseline justify-between gap-3 border-t border-dashed pt-3"
        >
          <dt className="font-display text-base font-extrabold">{total.label}</dt>
          <dd className="font-display text-xl font-extrabold tabular-nums">{total.value}</dd>
        </div>
      </dl>
      {notes && <div className="text-text-muted grid gap-1.5 text-xs">{notes}</div>}
      {children}
    </section>
  );
}

// ---------- StickyActionBar ----------
export interface StickyActionBarProps extends Omit<ComponentProps<"div">, "children"> {
  /** Small caps line above the amount ("Estimated total"). */
  summary?: ReactNode;
  /** The server's amount, already formatted. */
  amount?: ReactNode;
  /** The main button or link (Place order). */
  action: ReactNode;
  /** Accessible name of the region. Default "Order total". */
  label?: string;
}

/**
 * The bar that stays at the bottom of the viewport while the page scrolls (cart and checkout): the server's total
 * on the left and the Place order action on the right. It sticks within its column, so it never covers the footer.
 */
export function StickyActionBar({
  summary,
  amount,
  action,
  label = "Order total",
  className,
  ...props
}: StickyActionBarProps) {
  return (
    <div
      role="region"
      aria-label={label}
      className={cn(
        "border-border bg-surface-paper text-text sticky bottom-0 z-[1000] -mx-4 flex items-center justify-between gap-3 border-t px-4 py-3 pb-[calc(12px+env(safe-area-inset-bottom,0px))] shadow-[0_-6px_18px_rgb(40_30_10/0.08)] sm:mx-0 sm:rounded-[18px] sm:border sm:pb-3",
        className,
      )}
      {...props}
    >
      <div className="min-w-0">
        {summary && (
          <p className="text-text-muted text-[0.6875rem] font-semibold tracking-[0.04em] uppercase">
            {summary}
          </p>
        )}
        {amount && (
          <p className="font-display text-xl leading-tight font-extrabold tabular-nums">{amount}</p>
        )}
      </div>
      <div className="shrink-0">{action}</div>
    </div>
  );
}

// ---------- StatusSteps ----------
export type StatusStepState = "done" | "current" | "upcoming";

export interface StatusStep {
  key: string;
  label: ReactNode;
  /** When the server recorded this step, already formatted. */
  time?: ReactNode;
  state: StatusStepState;
}

export interface StatusStepsProps extends Omit<ComponentProps<"ol">, "children"> {
  steps: readonly StatusStep[];
  /** Accessible name. Default "Order progress". */
  label?: string;
}

/**
 * Order tracking steps: teal filled dots for steps done, a ringed dot for the current one, hollow for the rest.
 * Across the card from `sm` up, down the card on phones. Which step is current is the server's status.
 */
export function StatusSteps({
  steps,
  label = "Order progress",
  className,
  ...props
}: StatusStepsProps) {
  return (
    <ol
      aria-label={label}
      className={cn("grid gap-0 sm:auto-cols-fr sm:grid-flow-col sm:gap-2", className)}
      {...props}
    >
      {steps.map((step, i) => {
        const last = i === steps.length - 1;
        return (
          <li
            key={step.key}
            data-state={step.state}
            aria-current={step.state === "current" ? "step" : undefined}
            className="relative flex gap-3 pb-5 last:pb-0 sm:grid sm:justify-items-center sm:gap-1.5 sm:pb-0 sm:text-center"
          >
            {!last && (
              <span
                aria-hidden="true"
                className={cn(
                  "absolute top-6 bottom-0 left-[11px] w-[3px] rounded-full sm:top-[10px] sm:right-[calc(-50%-4px)] sm:bottom-auto sm:left-[calc(50%+16px)] sm:h-[3px] sm:w-auto",
                  step.state === "done" ? "bg-accent" : "bg-border",
                )}
              />
            )}
            <span
              aria-hidden="true"
              className={cn(
                "relative z-[1] grid size-6 shrink-0 place-items-center rounded-full border-[3px]",
                step.state === "done" && "border-accent bg-accent text-on-accent",
                step.state === "current" &&
                  "border-accent bg-surface-paper shadow-[0_0_0_4px_var(--fo-color-teal-soft)]",
                step.state === "upcoming" && "border-border bg-surface-paper",
              )}
            >
              {step.state === "done" && (
                <svg
                  viewBox="0 0 24 24"
                  className="size-3.5"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={3.5}
                >
                  <path d="M5 12.5 10 17l9-10" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              )}
              {step.state === "current" && <span className="bg-accent size-2 rounded-full" />}
            </span>
            <span className="grid min-w-0 gap-0.5">
              <span
                className={cn(
                  "text-sm leading-tight",
                  step.state === "upcoming" ? "text-text-muted font-semibold" : "font-bold",
                )}
              >
                {step.label}
                <span className="sr-only">
                  {step.state === "done" ? " (done)" : step.state === "current" ? " (now)" : ""}
                </span>
              </span>
              {step.time && <span className="text-text-muted text-xs">{step.time}</span>}
            </span>
          </li>
        );
      })}
    </ol>
  );
}
