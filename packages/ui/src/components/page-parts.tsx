// Page parts of the Bold blocks look (D88, D89, D181) shared by the admin and any page that needs them (D207: every
// reusable part lives in a package; screens only compose). Router-free: links come in as slots.
//
// None of these compute business values (CLAUDE.md rule 4): figures and labels are passed in already decided.
import { type ComponentProps, type ReactNode, createElement } from "react";
import { cn } from "../lib/cn";
import { type BlockTone } from "./bold-blocks";
import { Skeleton } from "./display";
import { Script } from "./heading";

type HeadingLevel = 1 | 2 | 3 | 4;
const heading = (level: HeadingLevel) => `h${level}` as const;

const blockTones: Record<BlockTone, string> = {
  coral: "bg-block-coral text-on-block-coral",
  teal: "bg-block-teal text-on-block-teal",
  mango: "bg-block-mango text-on-block-mango",
  ice: "bg-block-ice text-on-block-ice",
};

// ---------- Wordmark ----------
export interface WordmarkProps extends Omit<ComponentProps<"span">, "children"> {
  /** The brand mark picture (D119), served by the app (for example "/brand/mark.png"). */
  markSrc: string;
  /** Mark only, with the name for screen readers (a collapsed sidebar). */
  compact?: boolean;
  /** `default` ink text with a teal handwritten "Orbitt"; `inherit` keeps the surrounding colour (on a block). */
  tone?: "default" | "inherit";
}

/** "Fresh Orbitt" with "Orbitt" handwritten (D89), beside the brand mark in a paper circle with an ink ring. */
export function Wordmark({
  markSrc,
  compact = false,
  tone = "default",
  className,
  ...props
}: WordmarkProps) {
  return (
    <span className={cn("flex items-center gap-2 whitespace-nowrap", className)} {...props}>
      <span
        aria-hidden="true"
        className="border-outline-ink bg-surface-paper size-9 shrink-0 overflow-hidden rounded-full border-[3px]"
      >
        <img src={markSrc} alt="" width={64} height={64} className="size-full object-cover" />
      </span>
      {compact ? (
        <span className="sr-only">Fresh Orbitt</span>
      ) : (
        <span
          className={cn(
            "font-display text-[1.4375rem] leading-none font-extrabold",
            tone === "default" && "text-text",
          )}
        >
          Fresh{" "}
          <Script tone={tone === "default" ? "accent" : "inherit"} className="text-[1.1em]">
            Orbitt
          </Script>
        </span>
      )}
    </span>
  );
}

// ---------- PageHeader ----------
export interface PageHeaderProps extends Omit<ComponentProps<"div">, "title"> {
  title: ReactNode;
  description?: ReactNode;
  /** Buttons on the right. */
  actions?: ReactNode;
  /** A back link above the title (the app's router link). */
  back?: ReactNode;
  /** Badges next to the title (status). */
  meta?: ReactNode;
}

/** A page's h1 in Baloo 2 800 with the hand-drawn dash doodle, a line under it and the page's actions. */
export function PageHeader({
  title,
  description,
  actions,
  back,
  meta,
  className,
  ...props
}: PageHeaderProps) {
  return (
    <div className={cn("mb-6 flex flex-col gap-3", className)} {...props}>
      {back}
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display fo-doodle pr-[1em] text-3xl leading-tight font-extrabold tracking-[-0.01em] sm:text-4xl">
              {title}
            </h1>
            {meta}
          </div>
          {description && <p className="text-text-muted text-sm">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}

/** Classes for a back link above a page title, for the app's router link. */
export const backLinkClass =
  "text-text-muted hover:text-text inline-flex w-fit items-center gap-1 text-sm font-semibold [&_svg]:size-4";

// ---------- SectionCard ----------
export interface SectionCardProps extends Omit<ComponentProps<"section">, "title"> {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  /** Default 2. */
  headingLevel?: HeadingLevel;
}

/** A paper card with 24 px corners, a Baloo 2 800 title, an optional line and actions on the right. */
export function SectionCard({
  title,
  description,
  actions,
  headingLevel = 2,
  className,
  children,
  ...props
}: SectionCardProps) {
  return (
    <section
      className={cn(
        "border-outline-ink/10 bg-surface-paper shadow-soft-sm flex flex-col gap-4 rounded-3xl border p-5 sm:p-6",
        className,
      )}
      {...props}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-0.5">
          {createElement(
            heading(headingLevel),
            { className: "font-display text-xl leading-7 font-extrabold tracking-[-0.01em]" },
            title,
          )}
          {description && <p className="text-text-muted text-sm">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </section>
  );
}

// ---------- DetailList ----------
export interface DetailListProps {
  items: readonly { label: string; value: ReactNode }[];
  columns?: 1 | 2 | 3;
  className?: string;
}

/** Label / value pairs for detail pages; a missing value shows "—". */
export function DetailList({ items, columns = 2, className }: DetailListProps) {
  const grid = columns === 3 ? "sm:grid-cols-3" : columns === 2 ? "sm:grid-cols-2" : "";
  return (
    <dl className={cn("grid grid-cols-1 gap-x-6 gap-y-3", grid, className)}>
      {items.map((item) => (
        <div key={item.label} className="flex min-w-0 flex-col gap-0.5">
          <dt className="text-text-muted text-xs font-bold tracking-[0.08em] uppercase">
            {item.label}
          </dt>
          <dd className="text-sm break-words">{item.value ?? "—"}</dd>
        </div>
      ))}
    </dl>
  );
}

// ---------- StatTile ----------
export type StatTileTone = BlockTone | "paper";

export interface StatTileProps extends Omit<ComponentProps<"div">, "children"> {
  label: ReactNode;
  /** The figure, already formatted. */
  value: ReactNode;
  /** One small line under the figure. */
  sub?: ReactNode;
  /** A block colour, or `paper` (default) so `emphasis` colours stay readable. */
  tone?: StatTileTone;
  /** Warning or danger colour for the figure; only on paper. */
  emphasis?: "warning" | "danger";
  /** Shows a placeholder instead of the figure. */
  loading?: boolean;
}

/** A dashboard figure on a colour block or paper: label, the figure in Baloo 2 800, one line. */
export function StatTile({
  label,
  value,
  sub,
  tone = "paper",
  emphasis,
  loading = false,
  className,
  ...props
}: StatTileProps) {
  const onPaper = tone === "paper";
  return (
    <div
      data-tone={tone}
      className={cn(
        "shadow-soft-sm flex h-full flex-col gap-1 rounded-3xl p-4 transition-transform motion-safe:hover:-translate-y-0.5",
        onPaper ? "border-outline-ink/10 bg-surface-paper text-text border" : blockTones[tone],
        className,
      )}
      {...props}
    >
      <span className={cn("text-sm font-bold", onPaper && "text-text-muted")}>{label}</span>
      {loading ? (
        <Skeleton className="h-8 w-20" />
      ) : (
        <span
          className={cn(
            "font-display text-3xl leading-9 font-extrabold tracking-[-0.01em] tabular-nums",
            onPaper && emphasis === "danger" && "text-danger-text",
            onPaper && emphasis === "warning" && "text-warning-text",
          )}
        >
          {value}
        </span>
      )}
      {sub && <span className={cn("text-xs font-semibold", onPaper && "text-text-muted")}>{sub}</span>}
    </div>
  );
}

// ---------- BlockSplitCard ----------
export interface BlockSplitCardProps extends Omit<ComponentProps<"div">, "children"> {
  tone: BlockTone;
  /** The colour-block side (md and wider): a line and a character. Hidden on phones so the main side comes first. */
  aside: ReactNode;
  /** The paper side: a heading and a form. */
  children: ReactNode;
}

/** A paper card with 34 px corners split into a colour block and a paper side (sign-in). */
export function BlockSplitCard({ tone, aside, children, className, ...props }: BlockSplitCardProps) {
  return (
    <div
      className={cn(
        "bg-surface-paper text-text shadow-soft-lg grid w-full max-w-sm overflow-hidden rounded-[34px] md:max-w-3xl md:grid-cols-2",
        className,
      )}
      {...props}
    >
      <div
        className={cn(
          "hidden flex-col items-center justify-center gap-6 p-8 text-center md:flex",
          blockTones[tone],
        )}
      >
        {aside}
      </div>
      <div className="flex flex-col justify-center py-2">{children}</div>
    </div>
  );
}
