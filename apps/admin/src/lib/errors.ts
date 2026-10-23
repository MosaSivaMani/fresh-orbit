import { type ApiError, isApiError } from "@fresh/core";
import { type FieldValues, type Path, type UseFormSetError } from "react-hook-form";

/**
 * Problem Details field keys are camelCase JSON paths (`items[1].quantity.value`); react-hook-form uses dots
 * (`items.1.quantity.value`).
 */
export function toFormPath(apiField: string): string {
  return apiField.replace(/\[(\d+)\]/g, ".$1");
}

/**
 * Put the server's field errors on the form fields (docs/design §6.6). Returns the messages that matched no
 * known field, so the caller can show them in a banner. `aliases` maps API field paths to form paths when the
 * form names differ (e.g. `quantity.value` → `quantity`).
 */
export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  knownFields: readonly string[],
  aliases: Readonly<Record<string, string>> = {},
): string[] {
  if (!isApiError(error)) return [];
  const unmatched: string[] = [];
  let first = true;
  for (const [apiField, list] of Object.entries(error.errors)) {
    const message = list[0]?.message;
    if (!message) continue;
    const path = aliases[apiField] ?? toFormPath(apiField);
    const known = knownFields.some((f) => path === f || path.startsWith(`${f}.`));
    if (known) {
      setError(
        path as Path<T>,
        { type: list[0]?.code ?? "server", message },
        { shouldFocus: first },
      );
      first = false;
    } else {
      unmatched.push(message);
    }
  }
  return unmatched;
}

/** True when the error carries field errors (shown at the fields, not as a toast). */
export function hasFieldErrors(error: unknown): error is ApiError {
  return isApiError(error) && Object.keys(error.errors).length > 0;
}

const CODE_MESSAGES: Readonly<Record<string, string>> = {
  NETWORK_ERROR: "The server could not be reached. Check your connection and try again.",
  FORBIDDEN: "You do not have permission to do this.",
  LOCATION_ACCESS_DENIED: "You do not have access to this location.",
  CONCURRENCY_CONFLICT:
    "This record changed since you opened it. Reload to see the latest version.",
  CONCURRENT_SAVE: "Someone saved at the same moment. Try again.",
  VERSION_REQUIRED: "Reload this page and try again.",
  RATE_LIMITED: "Too many requests. Wait a moment and try again.",
  IDEMPOTENCY_IN_PROGRESS: "This request is still being processed. Wait a moment.",
  INTERNAL_ERROR: "Something went wrong on the server.",
  SERVICE_UNAVAILABLE: "A service is temporarily unavailable. Try again shortly.",
};

/**
 * One sentence for a toast or banner. Uses the server's `detail` (one sentence naming what to fix) and falls
 * back to a generic sentence per code. Never parses the text.
 */
export function errorMessage(error: unknown): string {
  if (!isApiError(error)) return "Something went wrong. Please try again.";
  const fallback = CODE_MESSAGES[error.code];
  if (error.code === "VALIDATION_FAILED") {
    const first = Object.values(error.errors)[0]?.[0]?.message;
    return first ?? error.problem.detail ?? "Some fields are invalid.";
  }
  return error.problem.detail ?? fallback ?? error.message;
}

export function isConflict(error: unknown): boolean {
  return isApiError(error) && error.code === "CONCURRENCY_CONFLICT";
}

export function isForbidden(error: unknown): boolean {
  return isApiError(error) && error.status === 403;
}

export function isNotFound(error: unknown): boolean {
  return isApiError(error) && error.status === 404;
}
