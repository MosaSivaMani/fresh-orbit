import { isApiError } from "@fresh/core";
import { toast } from "@fresh/ui";
import { type FieldValues, type UseFormSetError } from "react-hook-form";
import { showConflict } from "./conflict";
import { applyFieldErrors, errorMessage } from "./errors";

export interface ReportErrorOptions<T extends FieldValues> {
  /** Re-read the record after a 412 (usually `query.refetch`). */
  onConflict?: () => unknown;
  /** Form to receive field errors. */
  setError?: UseFormSetError<T>;
  fields?: readonly string[];
  aliases?: Readonly<Record<string, string>>;
  /** Title of the toast for refusals that are not field errors. */
  title?: string;
}

/**
 * Show a failed write the way docs/design §6.6 says: field errors at the fields, 412 → reload prompt,
 * everything else → one toast with the server's sentence and trace id.
 */
export function reportError<T extends FieldValues>(
  error: unknown,
  options: ReportErrorOptions<T> = {},
): void {
  if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT") {
    showConflict({ onReload: options.onConflict ?? (() => undefined) });
    return;
  }
  if (isApiError(error) && error.status === 401) return; // Session handling redirects to login.
  if (options.setError && options.fields) {
    const unmatched = applyFieldErrors(
      error,
      options.setError,
      options.fields,
      options.aliases ?? {},
    );
    const fieldCount = isApiError(error) ? Object.keys(error.errors).length : 0;
    if (fieldCount > 0 && unmatched.length === 0) return;
    if (unmatched.length > 0) {
      toast({
        title: options.title ?? "Could not save",
        description: unmatched.join(" "),
        tone: "danger",
      });
      return;
    }
  }
  const traceId = isApiError(error) ? error.traceId : undefined;
  toast({
    title: options.title ?? "Could not complete the action",
    description: traceId ? `${errorMessage(error)} Reference: ${traceId}` : errorMessage(error),
    tone: "danger",
  });
}

export function notifySaved(title: string, description?: string): void {
  toast({ title, tone: "success", ...(description ? { description } : {}) });
}
