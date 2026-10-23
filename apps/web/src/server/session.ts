// Backend-for-frontend session handlers (`/bff/auth/*`). See docs/design/web.md §4 for the security reasoning.
//
// - The refresh token never reaches browser scripts: it lives in an httpOnly, SameSite=Strict cookie scoped to
//   /bff/auth, and only these handlers read it.
// - The access token (15 min) is returned in the JSON body and kept in browser memory only.
// - Every handler requires a same-origin request carrying `X-Fresh-Client: web` (a custom header cannot be sent
//   cross-site without a CORS preflight, which this site never allows), on top of SameSite=Strict.
import {
  type BrowserSession,
  type OtpVerifyResponse,
  type RefreshResponse,
} from "@/api/types/auth";
import { CART_COOKIE, REFRESH_COOKIE, SESSION_HINT_COOKIE } from "@/lib/cookies";
import { ConfigurationMissingError, getApiOrigin, isProduction } from "./config";
import { problemResponse } from "./problem";

export const CLIENT_HEADER = "x-fresh-client";

export interface CookieJar {
  get(name: string): { value: string } | undefined;
  set(name: string, value: string, options: CookieOptions): void;
  delete(options: { name: string; path?: string }): void;
}

export interface CookieOptions {
  httpOnly?: boolean;
  sameSite?: "strict" | "lax";
  secure?: boolean;
  path?: string;
  expires?: Date;
  maxAge?: number;
}

const REFRESH_COOKIE_PATH = "/bff/auth";

/** Refuses cross-site and scripted-from-elsewhere calls. */
export function checkSameOrigin(request: Request): Response | null {
  if (request.headers.get(CLIENT_HEADER) !== "web") {
    return problemResponse(403, "FORBIDDEN", "This request is not allowed.");
  }
  const origin = request.headers.get("origin");
  if (origin !== null && origin !== new URL(request.url).origin) {
    return problemResponse(403, "FORBIDDEN", "This request is not allowed.");
  }
  const site = request.headers.get("sec-fetch-site");
  if (site !== null && site !== "same-origin") {
    return problemResponse(403, "FORBIDDEN", "This request is not allowed.");
  }
  return null;
}

function apiUrl(path: string): string {
  return `${getApiOrigin()}/api/v1${path}`;
}

function forwardHeaders(request: Request): Headers {
  const headers = new Headers({ "Content-Type": "application/json", Accept: "application/json" });
  const forwardedFor = request.headers.get("x-forwarded-for") ?? request.headers.get("x-real-ip");
  if (forwardedFor) headers.set("X-Forwarded-For", forwardedFor);
  const correlation = request.headers.get("x-correlation-id");
  if (correlation) headers.set("X-Correlation-Id", correlation);
  return headers;
}

function storeRefreshToken(jar: CookieJar, token: string, expiresAt: string) {
  const expires = new Date(expiresAt);
  jar.set(REFRESH_COOKIE, token, {
    httpOnly: true,
    sameSite: "strict",
    secure: isProduction,
    path: REFRESH_COOKIE_PATH,
    expires,
  });
  jar.set(SESSION_HINT_COOKIE, "1", {
    httpOnly: false,
    sameSite: "lax",
    secure: isProduction,
    path: "/",
    expires,
  });
}

function clearSessionCookies(jar: CookieJar) {
  jar.delete({ name: REFRESH_COOKIE, path: REFRESH_COOKIE_PATH });
  jar.delete({ name: SESSION_HINT_COOKIE, path: "/" });
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
  });
}

/** Passes an API refusal through unchanged (status + Problem Details body). */
async function passThrough(upstream: Response): Promise<Response> {
  return new Response(await upstream.text(), {
    status: upstream.status,
    headers: {
      "Content-Type": upstream.headers.get("content-type") ?? "application/problem+json",
      "Cache-Control": "no-store",
      ...(upstream.headers.get("retry-after")
        ? { "Retry-After": upstream.headers.get("retry-after") as string }
        : {}),
    },
  });
}

async function callApi(
  fetchImpl: typeof fetch,
  path: string,
  init: RequestInit,
): Promise<Response | { unavailable: Response }> {
  try {
    return await fetchImpl(apiUrl(path), { ...init, cache: "no-store" });
  } catch (error) {
    if (error instanceof ConfigurationMissingError) {
      return {
        unavailable: problemResponse(503, "SERVICE_UNAVAILABLE", "The shop is not configured."),
      };
    }
    return {
      unavailable: problemResponse(
        503,
        "SERVICE_UNAVAILABLE",
        "The shop is not reachable right now.",
      ),
    };
  }
}

interface VerifyInput {
  challengeId?: unknown;
  phone?: unknown;
  code?: unknown;
}

/** `POST /bff/auth/verify`: OTP verify + guest cart merge; sets the refresh cookie; returns the access token. */
export async function verifyOtp(
  request: Request,
  jar: CookieJar,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  const refused = checkSameOrigin(request);
  if (refused) return refused;
  let input: VerifyInput;
  try {
    input = (await request.json()) as VerifyInput;
  } catch {
    return problemResponse(400, "MALFORMED_REQUEST", "The request body is not valid JSON.");
  }
  const body = {
    challengeId: typeof input.challengeId === "string" ? input.challengeId : "",
    phone: typeof input.phone === "string" ? input.phone : "",
    code: typeof input.code === "string" ? input.code : "",
    deviceLabel: "Website",
    guestCartToken: jar.get(CART_COOKIE)?.value ?? null,
  };
  const upstream = await callApi(fetchImpl, "/auth/otp/verify", {
    method: "POST",
    headers: forwardHeaders(request),
    body: JSON.stringify(body),
  });
  if ("unavailable" in upstream) return upstream.unavailable;
  if (!upstream.ok) return passThrough(upstream);

  const data = (await upstream.json()) as OtpVerifyResponse;
  storeRefreshToken(jar, data.refreshToken, data.refreshTokenExpiresAt);
  // The guest cart is merged into the customer's cart by the API (CART-R27..R30); the guest token is spent.
  jar.delete({ name: CART_COOKIE, path: "/" });
  const session: BrowserSession & { cart: OtpVerifyResponse["cart"] } = {
    accessToken: data.accessToken,
    accessTokenExpiresAt: data.accessTokenExpiresAt,
    user: data.user,
    cart: data.cart,
  };
  return json(session);
}

/** `POST /bff/auth/refresh`: rotates the refresh cookie and returns a new access token. */
export async function refreshSession(
  request: Request,
  jar: CookieJar,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  const refused = checkSameOrigin(request);
  if (refused) return refused;
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    clearSessionCookies(jar);
    return problemResponse(401, "REFRESH_TOKEN_INVALID", "Please sign in again.");
  }
  const upstream = await callApi(fetchImpl, "/auth/refresh", {
    method: "POST",
    headers: forwardHeaders(request),
    body: JSON.stringify({ refreshToken }),
  });
  if ("unavailable" in upstream) return upstream.unavailable;
  if (!upstream.ok) {
    if (upstream.status === 401) clearSessionCookies(jar);
    return passThrough(upstream);
  }
  const data = (await upstream.json()) as RefreshResponse;
  storeRefreshToken(jar, data.refreshToken, data.refreshTokenExpiresAt);
  const session: BrowserSession = {
    accessToken: data.accessToken,
    accessTokenExpiresAt: data.accessTokenExpiresAt,
    user: null,
  };
  return json(session);
}

/** `POST /bff/auth/logout`: revokes the refresh token at the API (best effort) and clears the cookies. */
export async function logout(
  request: Request,
  jar: CookieJar,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  const refused = checkSameOrigin(request);
  if (refused) return refused;
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  const authorization = request.headers.get("authorization");
  if (refreshToken && authorization) {
    const headers = forwardHeaders(request);
    headers.set("Authorization", authorization);
    await callApi(fetchImpl, "/auth/logout", {
      method: "POST",
      headers,
      body: JSON.stringify({ refreshToken, allDevices: false }),
    });
  }
  clearSessionCookies(jar);
  return new Response(null, { status: 204, headers: { "Cache-Control": "no-store" } });
}
