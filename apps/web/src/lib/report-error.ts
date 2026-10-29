// Browser half of error monitoring (W-7, D316): an error boundary posts what it caught to this site's
// /bff/monitoring/errors, and the server hands it to the configured reporter (none by default). Same origin, so the
// CSP stays `connect-src 'self'` and no third-party script runs in the page.
import { isApiError } from "@fresh/core";

const ENDPOINT = "/bff/monitoring/errors";
const MAX_STACK = 8000;

export interface BrowserErrorContext {
  /** Which boundary caught it: `route` (app/error.tsx) or `global` (app/global-error.tsx). */
  boundary: "route" | "global";
}

/** Sends one report; never throws and never blocks the page. */
export function reportBrowserError(
  error: Error & { digest?: string },
  context: BrowserErrorContext,
): void {
  if (typeof window === "undefined") return;
  const body = JSON.stringify({
    name: error.name,
    message: error.message,
    stack: error.stack?.slice(0, MAX_STACK),
    digest: error.digest,
    traceId: isApiError(error) ? error.traceId : undefined,
    path: window.location.pathname,
    boundary: context.boundary,
  });
  try {
    void fetch(new URL(ENDPOINT, window.location.origin), {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Fresh-Client": "web" },
      credentials: "same-origin",
      keepalive: true,
      body,
    }).catch(() => {});
  } catch {
    // Monitoring must never break the error page.
  }
}
