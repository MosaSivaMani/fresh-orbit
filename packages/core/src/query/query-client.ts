import { QueryClient, type QueryClientConfig } from "@tanstack/react-query";
import { isApiError } from "../http/api-error";

const MAX_RETRIES = 2;

/** Retry only what can succeed unchanged: network failures, 429, 503 and "in progress" conflicts. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= MAX_RETRIES) return false;
  if (isApiError(error)) return error.isRetryable;
  return false;
}

/** Exponential back-off that honours `Retry-After`. */
export function retryDelay(attempt: number, error: unknown): number {
  if (isApiError(error) && error.retryAfterSeconds !== undefined) {
    return Math.min(error.retryAfterSeconds * 1000, 30_000);
  }
  return Math.min(500 * 2 ** attempt, 8_000);
}

/**
 * QueryClient with Fresh Orbit defaults: 30 s stale time, retries only for retryable ApiErrors,
 * no refetch on window focus for mutations, and no automatic mutation retries
 * (writes are retried by the user with the same Idempotency-Key).
 */
export function createQueryClient(config: QueryClientConfig = {}): QueryClient {
  return new QueryClient({
    ...config,
    defaultOptions: {
      ...config.defaultOptions,
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        retry: shouldRetry,
        retryDelay,
        refetchOnWindowFocus: true,
        ...config.defaultOptions?.queries,
      },
      mutations: {
        retry: false,
        ...config.defaultOptions?.mutations,
      },
    },
  });
}
