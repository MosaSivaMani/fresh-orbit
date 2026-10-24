import { humanizeCode } from "@fresh/core";
import { type VariantProps, cva } from "class-variance-authority";
import { AlertTriangle, Inbox, RotateCw } from "lucide-react";
import { type ComponentProps, type ReactNode, createElement } from "react";
import { cn } from "../lib/cn";
import { Button } from "./button";
import { Illustration, type IllustrationSize, type IllustrationSlot } from "./illustrated";

// ---------- Card ----------
export function Card({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      className={cn(
        "border-border bg-surface text-text shadow-soft-md rounded-3xl border",
        className,
      )}
      {...props}
    />
  );
}

export function CardHeader({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("flex flex-col gap-1 p-5 pb-3", className)} {...props} />;
}

export function CardTitle({ className, children, ...props }: ComponentProps<"h3">) {
  return (
    <h3 className={cn("font-display text-lg leading-7 font-bold", className)} {...props}>
      {children}
    </h3>
  );
}

export function CardDescription({ className, ...props }: ComponentProps<"p">) {
  return <p className={cn("text-text-muted text-sm", className)} {...props} />;
}

export function CardContent({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("p-5 pt-0", className)} {...props} />;
}

export function CardFooter({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      className={cn("border-border flex items-center gap-2 border-t p-4", className)}
      {...props}
    />
  );
}

// ---------- Badge ----------
export const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs leading-4 font-semibold whitespace-nowrap [&_svg]:size-3",
  {
    variants: {
      tone: {
        neutral: "border-border bg-muted text-text",
        primary: "bg-primary text-on-primary border-transparent",
        accent: "bg-accent-subtle text-accent-text border-transparent",
        highlight: "bg-highlight-subtle text-highlight-text border-transparent",
        success: "bg-success-subtle text-success-text border-transparent",
        warning: "bg-warning-subtle text-warning-text border-transparent",
        danger: "bg-danger-subtle text-danger-text border-transparent",
        info: "bg-info-subtle text-info-text border-transparent",
      },
    },
    defaultVariants: { tone: "neutral" },
  },
);

export type BadgeTone = NonNullable<VariantProps<typeof badgeVariants>["tone"]>;

export interface BadgeProps extends ComponentProps<"span">, VariantProps<typeof badgeVariants> {}

export function Badge({ className, tone, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}

// ---------- StatusBadge ----------
export interface StatusStyle {
  label: string;
  tone: BadgeTone;
}

/** Map of UPPER_SNAKE status codes to label and tone, defined once per domain in the app. */
export type StatusMap = Readonly<Record<string, StatusStyle>>;

export interface StatusBadgeProps extends Omit<BadgeProps, "tone" | "children"> {
  code: string;
  map: StatusMap;
  /** Tone for codes missing from the map (label falls back to the humanised code). */
  fallbackTone?: BadgeTone;
  /** Show a leading dot. */
  dot?: boolean;
}

/** Renders a server status code with the label and colour from `map`; never decides the status itself. */
export function StatusBadge({
  code,
  map,
  fallbackTone = "neutral",
  dot = true,
  ...props
}: StatusBadgeProps) {
  const style = map[code];
  return (
    <Badge tone={style?.tone ?? fallbackTone} data-status={code} {...props}>
      {dot && <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />}
      {style?.label ?? humanizeCode(code)}
    </Badge>
  );
}

// ---------- Skeleton ----------
export function Skeleton({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      aria-hidden="true"
      className={cn("bg-muted animate-pulse rounded-md", className)}
      {...props}
    />
  );
}

// ---------- EmptyState ----------
export interface EmptyStateProps {
  title: ReactNode;
  description?: ReactNode;
  /** Small icon in a circle; used when no `slot` is given. */
  icon?: ReactNode;
  action?: ReactNode;
  className?: string;
  /** Illustration slot (D53), shown instead of the icon, for example "EMPTY_CART". */
  slot?: IllustrationSlot;
  /** Default `md` (240 px); use `sm` inside dense admin panels. */
  illustrationSize?: IllustrationSize;
  /** Default true: the title already says what the picture shows. */
  illustrationDecorative?: boolean;
  /** Render the title as a heading of this level; without it the title is a paragraph. */
  headingLevel?: 2 | 3 | 4;
}

export function EmptyState({
  title,
  description,
  icon,
  action,
  className,
  slot,
  illustrationSize = "md",
  illustrationDecorative = true,
  headingLevel,
}: EmptyStateProps) {
  const titleClass = slot
    ? "font-display text-text text-2xl leading-8 font-bold text-balance"
    : "font-display text-text text-lg leading-7 font-bold";
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-3 px-6 py-12 text-center",
        className,
      )}
    >
      {slot ? (
        <Illustration slot={slot} size={illustrationSize} decorative={illustrationDecorative} />
      ) : (
        <div className="bg-accent-subtle text-accent-text flex size-12 items-center justify-center rounded-full [&_svg]:size-6">
          {icon ?? <Inbox aria-hidden="true" />}
        </div>
      )}
      <div className="flex max-w-sm flex-col gap-1">
        {createElement(headingLevel ? `h${headingLevel}` : "p", { className: titleClass }, title)}
        {description && <p className="text-text-muted text-base">{description}</p>}
      </div>
      {action}
    </div>
  );
}

// ---------- ErrorState ----------
export interface ErrorStateProps {
  title?: ReactNode;
  description?: ReactNode;
  /** Shown small for support ("Reference: …"); pass ApiError.traceId. */
  traceId?: string | undefined;
  onRetry?: () => void;
  retryLabel?: string;
  className?: string;
}

export function ErrorState({
  title = "Something went wrong",
  description = "Please try again. If it keeps happening, contact support.",
  traceId,
  onRetry,
  retryLabel = "Try again",
  className,
}: ErrorStateProps) {
  return (
    <div
      role="alert"
      className={cn(
        "flex flex-col items-center justify-center gap-3 px-6 py-12 text-center",
        className,
      )}
    >
      <div className="bg-danger-subtle text-danger-text flex size-12 items-center justify-center rounded-full">
        <AlertTriangle aria-hidden="true" className="size-6" />
      </div>
      <div className="flex max-w-sm flex-col gap-1">
        <p className="font-display text-text text-lg leading-7 font-bold">{title}</p>
        {description && <p className="text-text-muted text-sm">{description}</p>}
        {traceId && (
          <p className="text-text-muted font-mono text-xs break-all">Reference: {traceId}</p>
        )}
      </div>
      {onRetry && (
        <Button
          variant="outline"
          size="sm"
          onClick={onRetry}
          leadingIcon={<RotateCw aria-hidden="true" />}
        >
          {retryLabel}
        </Button>
      )}
    </div>
  );
}
