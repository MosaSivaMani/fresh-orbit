// Illustrated states (U3, D53, D88). Characters appear only here: empty lists, "all caught up", no access (403),
// failed loads and 404. Each is the shared `CharacterState` from @fresh/ui (the character in a ringed circle, never a
// rectangle laid over a page) on a paper card; this file only picks the slot, words and action (D207).
import { Button, CharacterState, type IllustrationSlot, cn } from "@fresh/ui";
import { RotateCw } from "lucide-react";
import { type ReactNode } from "react";
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

export interface IllustratedStateProps {
  slot: Exclude<AdminSlot, "ADMIN_LOGIN">;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  /** Heading level of the title: 1 for page-level states, default 2. */
  headingLevel?: 1 | 2 | 3;
  /** `alert` for failed loads. */
  role?: "alert" | "status";
  /** Extra line under the description (trace id). */
  footnote?: ReactNode;
  className?: string;
}

/** A paper card holding the shared character state: the character, the title, a line of text and one action. */
export function IllustratedState({
  slot,
  title,
  description,
  action,
  headingLevel = 2,
  role,
  footnote,
  className,
}: IllustratedStateProps) {
  return (
    <CharacterState
      role={role}
      data-state-slot={slot}
      slot={slot}
      title={title}
      description={description}
      actions={action}
      headingLevel={headingLevel}
      className={cn(
        "border-outline-ink/10 bg-surface-paper shadow-soft-sm mx-auto my-4 w-full max-w-xl rounded-3xl border px-5 py-8 sm:py-10",
        className,
      )}
    >
      {footnote}
    </CharacterState>
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
    <div className={cn("flex items-center justify-center", fullPage && "bg-canvas min-h-dvh p-4")}>
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
