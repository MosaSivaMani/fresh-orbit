// Next.js instrumentation: every error thrown while rendering a page, in a route handler or in a server action goes
// to the configured error reporter (W-7, D316). With ERROR_REPORTING_DSN unset this does nothing.
import { reportRequestError } from "@/server/monitoring";

export function onRequestError(
  error: unknown,
  request: Readonly<{ path: string; method: string }>,
  context: Readonly<{ routePath: string; routeType: string }>,
): void {
  reportRequestError(error, request, context);
}
