"use client";

// Empty, error and not-found states in the food-app look (D95): the character in a ringed circle
// (`CharacterState` from @fresh/ui, one @fresh/illustrations slot per state), a heading, one line and the actions.
import { Button, CharacterState, type IllustrationSlot } from "@fresh/ui";
import { RotateCw } from "lucide-react";
import { type ReactNode } from "react";
import { asApiError, describeError } from "@/lib/errors";

/** A network or "shop unreachable" failure shows the offline character; anything else the generic one. */
export function errorSlot(error: unknown): IllustrationSlot {
  const code = asApiError(error)?.code;
  return code === "NETWORK_ERROR" || code === "SERVICE_UNAVAILABLE" ? "OFFLINE" : "ERROR_GENERIC";
}

export interface LoadErrorProps {
  title: ReactNode;
  error: unknown;
  onRetry?: () => void;
  /** Overrides the server's message. */
  description?: ReactNode;
  slot?: IllustrationSlot;
  headingLevel?: 1 | 2 | 3;
  actions?: ReactNode;
}

/** A read failed: the server's message, the support reference and a retry. */
export function LoadError({
  title,
  error,
  onRetry,
  description,
  slot,
  headingLevel = 2,
  actions,
}: LoadErrorProps) {
  const traceId = asApiError(error)?.traceId;
  return (
    <div role="alert">
      <CharacterState
        slot={slot ?? errorSlot(error)}
        avatarSize="xs"
        headingLevel={headingLevel}
        title={title}
        description={
          <>
            <p>{description ?? describeError(error)}</p>
            {traceId && <p className="mt-1 font-mono text-xs break-all">Reference: {traceId}</p>}
          </>
        }
        actions={
          (onRetry || actions) && (
            <>
              {onRetry && (
                <Button
                  variant="outline"
                  onClick={onRetry}
                  leadingIcon={<RotateCw aria-hidden="true" />}
                >
                  Try again
                </Button>
              )}
              {actions}
            </>
          )
        }
      />
    </div>
  );
}

export interface EmptyStateProps {
  slot: IllustrationSlot;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  headingLevel?: 1 | 2 | 3;
}

/** Nothing here yet: the slot's character, a heading, one line and what to do next. */
export function EmptyPage({
  slot,
  title,
  description,
  actions,
  headingLevel = 2,
}: EmptyStateProps) {
  return (
    <CharacterState
      slot={slot}
      headingLevel={headingLevel}
      title={title}
      description={description}
      actions={actions}
    />
  );
}
