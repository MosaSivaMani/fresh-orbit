import { ApiError, type FetchLike, apiErrorFromResponse } from "@fresh/core";
import createClient, { type Client, type ClientOptions } from "openapi-fetch";
import type { components, paths } from "./schema";

export type { components, paths };

/** Shorthand for a schema from the OpenAPI document: `Schema<"OrderDetailResponse">`. */
export type Schema<Name extends keyof SchemaMap> = SchemaMap[Name];
type SchemaMap = components extends { schemas: infer S } ? S : Record<string, never>;

export type FreshApiClient = Client<paths>;

export interface ApiClientOptions extends Omit<ClientOptions, "fetch" | "baseUrl"> {
  /**
   * API origin. Paths in the document already include `/api/v1`, so pass `https://api.freshorbit.in`,
   * not `.../api/v1`.
   */
  baseUrl: string;
  /** Use `createHttpClient(...).fetch` or `createAuthFetch(...)` from @fresh/core for bearer + refresh. */
  fetch?: FetchLike;
}

/** Typed client: `client.GET("/api/v1/catalog/products", { params: { query: { page: 1 } } })`. */
export function createApiClient(options: ApiClientOptions): FreshApiClient {
  const { fetch, ...rest } = options;
  return createClient<paths>({
    ...rest,
    ...(fetch ? { fetch: (request: Request) => fetch(request) } : {}),
  });
}

interface FetchResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/**
 * Turn an openapi-fetch result into data, or throw the same {@link ApiError} @fresh/core produces
 * (Problem Details → `{ status, code, errors, traceId }`).
 */
export async function unwrap<T>(result: Promise<FetchResult<T>> | FetchResult<T>): Promise<T> {
  const { data, error, response } = await result;
  if (!response.ok || error !== undefined) {
    throw apiErrorFromResponse(response, error ?? null);
  }
  return data as T;
}

export { ApiError };
