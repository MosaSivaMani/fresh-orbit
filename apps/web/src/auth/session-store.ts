// Browser session: the access token lives only in this module's memory (never in storage), the refresh token in an
// httpOnly cookie that only the /bff/auth handlers read. A page reload restores the session through /bff/auth/refresh.
import { ApiError, apiErrorFromResponse, readBody } from "@fresh/core";
import { type BrowserSession, type SessionUser } from "@/api/types/auth";
import { SESSION_HINT_COOKIE, readBrowserCookie } from "@/lib/cookies";

export type SessionStatus = "unknown" | "signedIn" | "signedOut";

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

/** One refresh at a time per tab (single flight) and across tabs (Web Locks). Resolves true when signed in. */
export function refreshSession(): Promise<boolean> {
  inflight ??= withCrossTabLock(async () => {
    try {
      const response = await fetch(bffUrl("/bff/auth/refresh"), {
        method: "POST",
        headers: BFF_HEADERS,
        credentials: "same-origin",
      });
      if (!response.ok) {
        endSession();
        return false;
      }
      startSession((await response.json()) as BrowserSession);
      return true;
    } catch {
      // Network failure: keep the current state; the next call tries again.
      if (state.status === "unknown") setState({ status: "signedOut", user: null });
      return false;
    }
  }).finally(() => {
    inflight = null;
  });
  return inflight;
}

/** Called once on page load: restores the session when the hint cookie says a refresh cookie exists. */
export async function bootstrapSession(): Promise<void> {
  if (state.status !== "unknown") return;
  if (readBrowserCookie(SESSION_HINT_COOKIE) !== "1") {
    setState({ status: "signedOut", user: null });
    return;
  }
  await refreshSession();
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
