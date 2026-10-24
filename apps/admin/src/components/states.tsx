// Illustrated states (U3, D53). Characters appear only here: empty lists, "all caught up", no access (403),
// failed loads and 404 — always inside a card, on a tinted panel, at size `sm`, never pasted over a page.
// Styling uses design-token classes only (surface-paper, teal-soft, surface-sand-strong, accent-coral-subtle,
// outline-ink, font-display), so a colour or font swap happens in @fresh/design-tokens / @fresh/ui alone.
import { Button, Illustration, type IllustrationSlot, cn } from "@fresh/ui";
import { RotateCw } from "lucide-react";
import { type ReactNode, createElement } from "react";
import { isRouteErrorResponse, useRouteError } from "react-router";

/** Admin uses only these slots (docs/design/illustrations.md §3). */
export type AdminSlot = Extract<
  IllustrationSlot,
  | "ADMIN_EMPTY"
  | "ADMIN_ALL_CAUGHT_UP"
  | "ADMIN_NO_ACCESS"
  | "ERROR_GENERIC"
  | "NOT_FOUND"
  | "ADMIN_LOGIN"
>;

/** Tinted panel behind the character, one per slot. */
const PANEL_TONE: Record<Exclude<AdminSlot, "ADMIN_LOGIN">, string> = {
  ADMIN_EMPTY: "bg-surface-sand-strong",
  ADMIN_ALL_CAUGHT_UP: "bg-teal-soft",
  ADMIN_NO_ACCESS: "bg-surface-sand-strong",
  ERROR_GENERIC: "bg-accent-coral-subtle",
  NOT_FOUND: "bg-teal-soft",
};

export interface IllustratedStateProps {
  slot: Exclude<AdminSlot, "ADMIN_LOGIN">;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  /** Render the title as a heading of this level (page-level states); otherwise a paragraph. */
  headingLevel?: 1 | 2 | 3;
  /** `alert` for failed loads. */
  role?: "alert" | "status";
  /** Extra line under the description (trace id). */
  footnote?: ReactNode;
  className?: string;
}

/** A compact card: character on a tinted panel, then the title, a line of text and one action. */
export function IllustratedState({
  slot,
  title,
  description,
  action,
  headingLevel,
  role,
  footnote,
  className,
}: IllustratedStateProps) {
  return (
    <div
      role={role}
      data-state-slot={slot}
      className={cn(
        "border-border bg-surface-paper mx-auto my-4 flex w-full max-w-xl flex-col items-center gap-5 rounded-2xl border p-5 text-center shadow-xs sm:flex-row sm:text-left",
        className,
      )}
    >
      <div
        className={cn("flex shrink-0 items-center justify-center rounded-xl p-2", PANEL_TONE[slot])}
      >
        <Illustration slot={slot} size="sm" decorative />
      </div>
      <div className="flex min-w-0 flex-col items-center gap-2 sm:items-start">
        {createElement(
          headingLevel ? `h${headingLevel}` : "p",
          { className: "font-display text-text text-xl leading-7 font-bold text-balance" },
          title,
        )}
        {description && <p className="text-text-muted text-sm">{description}</p>}
        {footnote}
        {action && <div className="mt-1 flex flex-wrap gap-2">{action}</div>}
      </div>
    </div>
  );
}

/** Empty list or page (`ADMIN_EMPTY`). */
export function ListEmpty({
  title,
  description,
  action,
}: {
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <IllustratedState slot="ADMIN_EMPTY" title={title} description={description} action={action} />
  );
}

/** Nothing left to do (`ADMIN_ALL_CAUGHT_UP`): no open payment issues, nothing to pick, nothing low on stock. */
export function AllCaughtUp({
  title,
  description,
  action,
}: {
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <IllustratedState
      slot="ADMIN_ALL_CAUGHT_UP"
      role="status"
      title={title}
      description={description}
      action={action}
    />
  );
}

/** Failed load (`ERROR_GENERIC`) with the trace id for support and a retry. */
export function LoadError({
  title = "Could not load this page",
  description = "Please try again. If it keeps happening, send the reference to support.",
  traceId,
  onRetry,
}: {
  title?: ReactNode;
  description?: ReactNode;
  traceId?: string | undefined;
  onRetry?: () => void;
}) {
  return (
    <IllustratedState
      slot="ERROR_GENERIC"
      role="alert"
      title={title}
      description={description}
      footnote={
        traceId ? (
          <p className="text-text-muted font-mono text-xs break-all">Reference: {traceId}</p>
        ) : undefined
      }
      action={
        onRetry ? (
          <Button
            variant="outline"
            size="sm"
            onClick={onRetry}
            leadingIcon={<RotateCw aria-hidden="true" />}
          >
            Try again
          </Button>
        ) : undefined
      }
    />
  );
}

/**
 * Router error boundary: a screen that throws (or a lazy chunk that fails to load after a deploy) shows a
 * failed-load card instead of a blank page. A 404 response from a loader shows the not-found card.
 */
export function RouteError({ fullPage = false }: { fullPage?: boolean }) {
  const error = useRouteError();
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  return (
    <div
      className={cn(
        "flex items-center justify-center",
        fullPage && "bg-surface-sand min-h-dvh p-4",
      )}
    >
      {notFound ? (
        <IllustratedState
          slot="NOT_FOUND"
          headingLevel={1}
          title="This page was not found"
          description="Check the address, or go back to the dashboard."
          action={
            <Button asChild variant="outline" size="sm">
              <a href="/">Go to dashboard</a>
            </Button>
          }
        />
      ) : (
        <LoadError
          title="This screen could not be shown"
          description="Reload the page. If it keeps happening, tell support what you were doing."
          onRetry={() => window.location.reload()}
        />
      )}
    </div>
  );
}
