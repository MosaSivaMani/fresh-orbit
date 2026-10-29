// Browser session: the access token lives only in this module's memory (never in storage), the refresh token in an
// httpOnly cookie that only the /bff/auth handlers read. A page reload restores the session through /bff/auth/refresh.
import { ApiError, apiErrorFromResponse, readBody } from "@fresh/core";
import { type BrowserSession, type SessionUser } from "@/api/types/auth";
import { SESSION_HINT_COOKIE, readBrowserCookie } from "@/lib/cookies";

/**
 * `unavailable`: the page could not restore the session because the shop did not answer (429, 5xx, offline). The
 * refresh cookie may still be good, so the customer is not signed out; RequireAuth offers a retry (D317).
 */
export type SessionStatus = "unknown" | "signedIn" | "signedOut" | "unavailable";

export interface SessionState {
  status: SessionStatus;
  user: SessionUser | null;
}

let accessToken: string | null = null;
// One frozen object: useSyncExternalStore needs the same snapshot on every call or it loops.
const serverState: SessionState = Object.freeze({ status: "unknown", user: null });
let state: SessionState = serverState;
const listeners = new Set<() => void>();
let inflight: Promise<boolean> | null = null;

function setState(next: SessionState) {
  state = next;
  for (const listener of listeners) listener();
}

export const sessionStore = {
  getState: (): SessionState => state,
  getServerState: (): SessionState => serverState,
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  getAccessToken: (): string | null => accessToken,
};

export function bffUrl(path: string): string {
  return new URL(path, window.location.origin).toString();
}

export const BFF_HEADERS = { "X-Fresh-Client": "web" } as const;

export function startSession(session: BrowserSession) {
  accessToken = session.accessToken;
  setState({ status: "signedIn", user: session.user ?? state.user });
}

export function endSession() {
  accessToken = null;
  setState({ status: "signedOut", user: null });
}

/** Serialises refreshes across tabs (refresh tokens rotate; two tabs racing would trip reuse detection). */
async function withCrossTabLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = typeof navigator !== "undefined" ? navigator.locks : undefined;
  if (!locks) return work();
  return locks.request("fresh-orbit-refresh", work) as Promise<T>;
}

/**
 * One refresh at a time per tab (single flight) and across tabs (Web Locks). Resolves true when signed in and false
 * only when the BFF says the refresh token is no good (401): that, and nothing else, ends the session. A rate limit,
 * a 5xx or no answer throws an `ApiError` and keeps the session, so a busy or restarting shop never signs the
 * customer out in the middle of checkout (W-6, D317).
 */
export function refreshSession(): Promise<boolean> {
  inflight ??= withCrossTabLock(async () => {
    let response: Response;
    try {
      response = await fetch(bffUrl("/bff/auth/refresh"), {
        method: "POST",
        headers: BFF_HEADERS,
        credentials: "same-origin",
      });
    } catch (cause) {
      throw new ApiError({
        status: 0,
        code: "NETWORK_ERROR",
        message: "The server could not be reached.",
        cause,
      });
    }
    if (response.status === 401) {
      endSession();
      return false;
    }
    if (!response.ok) throw apiErrorFromResponse(response, await readBody(response));
    startSession((await response.json()) as BrowserSession);
    return true;
  }).finally(() => {
    inflight = null;
  });
  return inflight;
}

/**
 * Called once on page load: restores the session when the hint cookie says a refresh cookie exists. When the shop
 * does not answer, the state becomes `unavailable` (not signed out) until {@link retrySession} gets through.
 */
export async function bootstrapSession(): Promise<void> {
  if (state.status !== "unknown") return;
  if (readBrowserCookie(SESSION_HINT_COOKIE) !== "1") {
    setState({ status: "signedOut", user: null });
    return;
  }
  await retrySession();
}

/** Tries to restore the session again (RequireAuth's retry). Never signs out unless the BFF answers 401. */
export async function retrySession(): Promise<void> {
  try {
    await refreshSession();
  } catch {
    if (state.status === "unknown" || state.status === "unavailable") {
      setState({ status: "unavailable", user: null });
    }
  }
}

/**
 * Verify the OTP through the website's BFF, which keeps the refresh token in an httpOnly cookie and hands the guest
 * cart token to the API for merging. Throws `ApiError` (e.g. `OTP_INVALID`) exactly as the API refused.
 */
export async function signInWithOtp(input: {
  challengeId: string;
  phone: string;
  code: string;
}): Promise<BrowserSession & { cart: { id: string; mergedLineCount: number } | null }> {
  let response: Response;
  try {
    response = await fetch(bffUrl("/bff/auth/verify"), {
      method: "POST",
      headers: { ...BFF_HEADERS, "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify(input),
    });
  } catch {
    throw new ApiError({ status: 0, code: "NETWORK_ERROR", message: "Network request failed." });
  }
  const body = await readBody(response);
  if (!response.ok) throw apiErrorFromResponse(response, body);
  const session = body as BrowserSession & {
    cart: { id: string; mergedLineCount: number } | null;
  };
  startSession(session);
  return session;
}

export async function signOut(): Promise<void> {
  const token = accessToken;
  try {
    await fetch(bffUrl("/bff/auth/logout"), {
      method: "POST",
      headers: { ...BFF_HEADERS, ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      credentials: "same-origin",
    });
  } finally {
    endSession();
  }
}

/** Test helper: reset module state between tests. */
export function resetSessionForTests(next: SessionState = { status: "unknown", user: null }) {
  accessToken = null;
  inflight = null;
  state = next;
}
