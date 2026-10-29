import {
  ApiError,
  CLIENT_ERROR_CODES,
  apiErrorFromResponse,
  isApiError,
  readBody,
} from "./api-error";
import { type AuthFetchOptions, type FetchLike, createAuthFetch } from "./auth-fetch";
import { parseETag, toETag } from "./headers";

export type QueryValue = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryValue | readonly QueryValue[]>;

export interface RequestOptions {
  /** Repeated keys for arrays (`status=A&status=B`); null/undefined dropped. */
  query?: QueryParams;
  /** JSON-serialised unless it is FormData, Blob, URLSearchParams or a string. */
  body?: unknown;
  headers?: HeadersInit;
  /** Resource version for `If-Match` (quoted automatically). */
  ifMatch?: string | number;
  /** `Idempotency-Key` for retryable writes. */
  idempotencyKey?: string;
  signal?: AbortSignal;
}

export interface ApiResponse<T> {
  data: T;
  status: number;
  /** Bare version from the `ETag` header, for the next `If-Match`. */
  version: string | undefined;
  headers: Headers;
}

export interface HttpClientOptions extends Partial<Omit<AuthFetchOptions, "fetch">> {
  /** Origin plus API prefix, e.g. `https://api.freshorbit.in/api/v1`. */
  baseUrl: string;
  fetch?: FetchLike;
  /** Headers sent on every request (e.g. `X-Pincode`, `X-Cart-Token`). Evaluated per request. */
  defaultHeaders?: () => HeadersInit | undefined;
}

export interface HttpClient {
  request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResponse<T>>;
  get<T>(path: string, options?: Omit<RequestOptions, "body">): Promise<T>;
  post<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  put<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  patch<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  delete<T = void>(path: string, options?: RequestOptions): Promise<T>;
  /** The authenticated fetch used underneath; pass it to @fresh/api-client. */
  fetch: FetchLike;
}

export function buildUrl(baseUrl: string, path: string, query?: QueryParams): string {
  const base = baseUrl.replace(/\/+$/, "");
  const suffix = path.startsWith("/") ? path : `/${path}`;
  const search = new URLSearchParams();
  for (const [key, raw] of Object.entries(query ?? {})) {
    const values = Array.isArray(raw) ? raw : [raw];
    for (const value of values) {
      if (value !== null && value !== undefined) search.append(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `${base}${suffix}?${qs}` : `${base}${suffix}`;
}

function isRawBody(body: unknown): body is BodyInit {
  return (
    typeof body === "string" ||
    (typeof FormData !== "undefined" && body instanceof FormData) ||
    (typeof Blob !== "undefined" && body instanceof Blob) ||
    body instanceof URLSearchParams ||
    body instanceof ArrayBuffer
  );
}

/**
 * Typed JSON client for FreshOrbit.Api. Non-2xx responses throw {@link ApiError}; transport failures throw
 * ApiError with status 0 and code `NETWORK_ERROR` (or `ABORTED`).
 */
export function createHttpClient(options: HttpClientOptions): HttpClient {
  const authFetch: FetchLike = options.getAccessToken
    ? createAuthFetch({
        getAccessToken: options.getAccessToken,
        ...(options.refresh ? { refresh: options.refresh } : {}),
        ...(options.shouldRefresh ? { shouldRefresh: options.shouldRefresh } : {}),
        ...(options.onAuthFailure ? { onAuthFailure: options.onAuthFailure } : {}),
        ...(options.correlationId ? { correlationId: options.correlationId } : {}),
        ...(options.fetch ? { fetch: options.fetch } : {}),
      })
    : (options.fetch ?? ((input, init) => globalThis.fetch(input, init)));

  async function request<T>(
    method: string,
    path: string,
    opts: RequestOptions = {},
  ): Promise<ApiResponse<T>> {
    const headers = new Headers(options.defaultHeaders?.());
    new Headers(opts.headers).forEach((value, key) => headers.set(key, value));
    if (!headers.has("Accept")) headers.set("Accept", "application/json");
    if (opts.ifMatch !== undefined) headers.set("If-Match", toETag(opts.ifMatch));
    if (opts.idempotencyKey) headers.set("Idempotency-Key", opts.idempotencyKey);

    let body: BodyInit | undefined;
    if (opts.body !== undefined) {
      if (isRawBody(opts.body)) {
        body = opts.body;
      } else {
        body = JSON.stringify(opts.body);
        if (!headers.has("Content-Type")) headers.set("Content-Type", "application/json");
      }
    }

    let response: Response;
    try {
      response = await authFetch(buildUrl(options.baseUrl, path, opts.query), {
        method,
        headers,
        ...(body !== undefined ? { body } : {}),
        ...(opts.signal ? { signal: opts.signal } : {}),
      });
    } catch (cause) {
      // A refresh that could not run (429, 5xx) arrives here as its own ApiError; keep its status and code.
      if (isApiError(cause)) throw cause;
      const aborted = cause instanceof DOMException && cause.name === "AbortError";
      throw new ApiError({
        status: 0,
        code: aborted ? CLIENT_ERROR_CODES.ABORTED : CLIENT_ERROR_CODES.NETWORK_ERROR,
        message: aborted ? "The request was cancelled." : "The server could not be reached.",
        cause,
      });
    }

    const data = await readBody(response);
    if (!response.ok) throw apiErrorFromResponse(response, data);
    return {
      data: data as T,
      status: response.status,
      version: parseETag(response.headers.get("ETag")),
      headers: response.headers,
    };
  }

  const dataOf = async <T>(p: Promise<ApiResponse<T>>): Promise<T> => (await p).data;

  return {
    request,
    get: (path, opts) => dataOf(request("GET", path, opts)),
    post: (path, body, opts) => dataOf(request("POST", path, { ...opts, body })),
    put: (path, body, opts) => dataOf(request("PUT", path, { ...opts, body })),
    patch: (path, body, opts) => dataOf(request("PATCH", path, { ...opts, body })),
    delete: (path, opts) => dataOf(request("DELETE", path, opts)),
    fetch: authFetch,
  };
}
