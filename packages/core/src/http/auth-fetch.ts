import { type AuthTokens, type TokenStore } from "./token-store";

export type FetchLike = (input: Request | string | URL, init?: RequestInit) => Promise<Response>;

export interface AuthFetchOptions {
  /** Returns the current access token, or null when signed out. */
  getAccessToken: () => string | null | Promise<string | null>;
  /**
   * Obtains a new access token (normally `POST /api/v1/auth/refresh`). Resolve true on success.
   * Concurrent 401s share one call (single flight).
   */
  refresh?: () => Promise<boolean>;
  /** Decides whether a 401 means "refresh and retry". Default: Problem Details `code` is `TOKEN_EXPIRED`. */
  shouldRefresh?: (response: Response) => Promise<boolean>;
  /** Called once when the session cannot be recovered (refresh failed, or still 401 after refresh). */
  onAuthFailure?: () => void | Promise<void>;
  /** Underlying fetch; defaults to the global fetch. */
  fetch?: FetchLike;
  /** Sent as `X-Correlation-Id` when present. */
  correlationId?: () => string | undefined;
}

async function isTokenExpired(response: Response): Promise<boolean> {
  try {
    const body = (await response.clone().json()) as { code?: unknown };
    return body.code === "TOKEN_EXPIRED";
  } catch {
    return false;
  }
}

/**
 * Wraps fetch with `Authorization: Bearer`, single-flight refresh on 401 and one retry.
 * The result is a drop-in `fetch`, so it also powers openapi-fetch in @fresh/api-client.
 */
export function createAuthFetch(options: AuthFetchOptions): FetchLike {
  const baseFetch: FetchLike = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const shouldRefresh = options.shouldRefresh ?? isTokenExpired;
  let inflight: Promise<boolean> | null = null;

  const refreshOnce = (): Promise<boolean> => {
    const refresh = options.refresh;
    if (!refresh) return Promise.resolve(false);
    inflight ??= Promise.resolve()
      .then(refresh)
      .catch(() => false)
      .finally(() => {
        inflight = null;
      });
    return inflight;
  };

  const send = (original: Request, token: string | null): Promise<Response> => {
    const headers = new Headers(original.headers);
    if (token) headers.set("Authorization", `Bearer ${token}`);
    const correlationId = options.correlationId?.();
    if (correlationId && !headers.has("X-Correlation-Id"))
      headers.set("X-Correlation-Id", correlationId);
    return baseFetch(new Request(original.clone(), { headers }));
  };

  return async (input, init) => {
    const original = new Request(input, init);
    const usedToken = await options.getAccessToken();
    const response = await send(original, usedToken);

    if (response.status !== 401 || !usedToken || !options.refresh) return response;
    if (!(await shouldRefresh(response))) return response;

    // Another request may already have rotated the token while this one was in flight.
    const current = await options.getAccessToken();
    const refreshed = current !== null && current !== usedToken ? true : await refreshOnce();
    if (!refreshed) {
      await options.onAuthFailure?.();
      return response;
    }

    const retried = await send(original, await options.getAccessToken());
    if (retried.status === 401) await options.onAuthFailure?.();
    return retried;
  };
}

export interface RefreshHandlerOptions {
  /** API origin plus prefix, e.g. `https://api.freshorbit.in/api/v1`. */
  baseUrl: string;
  tokenStore: TokenStore;
  /** Defaults to `/auth/refresh` (customer and staff share it). */
  path?: string;
  fetch?: FetchLike;
}

/**
 * Builds the `refresh` callback for {@link createAuthFetch}: posts the stored refresh token, stores the
 * rotated pair, and clears the store when the server refuses (REFRESH_TOKEN_INVALID / _REUSED).
 */
export function createRefreshHandler(options: RefreshHandlerOptions): () => Promise<boolean> {
  const baseFetch: FetchLike = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const url = `${options.baseUrl.replace(/\/+$/, "")}${options.path ?? "/auth/refresh"}`;
  return async () => {
    const refreshToken = await options.tokenStore.getRefreshToken();
    if (!refreshToken) return false;
    const response = await baseFetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (!response.ok) {
      await options.tokenStore.clear();
      return false;
    }
    await options.tokenStore.setTokens((await response.json()) as AuthTokens);
    return true;
  };
}
