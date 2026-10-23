// HTTP client for Client Components: same-origin calls to this site's /api/v1 proxy, Bearer token from memory,
// single-flight refresh through /bff/auth/refresh, all via @fresh/core.
import { createHttpClient, type HttpClient } from "@fresh/core";
import { endSession, refreshSession, sessionStore } from "@/auth/session-store";

let client: HttpClient | null = null;

export function browserHttp(): HttpClient {
  client ??= createHttpClient({
    baseUrl: `${window.location.origin}/api/v1`,
    getAccessToken: () => sessionStore.getAccessToken(),
    refresh: refreshSession,
    onAuthFailure: endSession,
  });
  return client;
}

/** Test helper: forget the cached client (e.g. after changing window.location). */
export function resetBrowserHttpForTests() {
  client = null;
}
