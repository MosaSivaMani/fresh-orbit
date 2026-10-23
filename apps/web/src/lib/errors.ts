// Turning ApiError into UI text. Logic branches on `code`; the text shown is the server's one-sentence message
// (docs/api §1.6) or, for transport problems, our own copy.
import { type ApiError, isApiError } from "@fresh/core";

export function asApiError(error: unknown): ApiError | null {
  return isApiError(error) ? error : null;
}

export function hasCode(error: unknown, ...codes: string[]): boolean {
  return isApiError(error) && codes.includes(error.code);
}

/** First server message for one field (`errors[field][0].message`). */
export function fieldMessage(error: unknown, ...fields: string[]): string | undefined {
  if (!isApiError(error)) return undefined;
  for (const field of fields) {
    const message = error.fieldMessage(field);
    if (message) return message;
  }
  return undefined;
}

/** A sentence for a toast or banner. */
export function describeError(error: unknown): string {
  if (!isApiError(error)) return "Something went wrong. Please try again.";
  switch (error.code) {
    case "NETWORK_ERROR":
    case "SERVICE_UNAVAILABLE":
      return "We could not reach the shop. Check your connection and try again.";
    case "RATE_LIMITED":
      return error.retryAfterSeconds
        ? `Too many attempts. Please wait ${error.retryAfterSeconds} seconds and try again.`
        : "Too many attempts. Please wait a moment and try again.";
    case "INTERNAL_ERROR":
      return "Something went wrong on our side. Please try again.";
    default: {
      const first = Object.values(error.errors)[0]?.[0]?.message;
      return error.problem.detail ?? first ?? error.problem.title ?? error.message;
    }
  }
}
