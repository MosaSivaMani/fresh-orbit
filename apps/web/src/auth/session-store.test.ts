import { type ApiError } from "@fresh/core";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { browserHttp } from "@/api/browser";
import { SESSION_HINT_COOKIE } from "@/lib/cookies";
import { problem } from "../../test/fixtures";
import { API, BFF } from "../../test/msw/handlers";
import { server } from "../../test/msw/server";
import { signedIn } from "../../test/render";
import {
  bootstrapSession,
  refreshSession,
  retrySession,
  sessionStore,
  startSession,
} from "./session-store";

const refreshed = {
  accessToken: "access-2",
  accessTokenExpiresAt: "2026-10-23T06:00:00Z",
  user: null,
};

describe("session store refresh (W-6)", () => {
  it.each([429, 500, 502, 503])(
    "keeps a signed-in customer when the refresh answers %i",
    async (status) => {
      startSession(signedIn);
      server.use(
        http.post(`${BFF}/refresh`, () =>
          HttpResponse.json(problem(status, `CODE_${status}`), {
            status,
            headers: { "Retry-After": "5" },
          }),
        ),
      );

      const error = (await refreshSession().catch((e: unknown) => e)) as ApiError;

      expect(error.status).toBe(status);
      expect(sessionStore.getState().status).toBe("signedIn");
      expect(sessionStore.getAccessToken()).toBe("access-1");
    },
  );

  it("keeps a signed-in customer when the refresh gets no answer", async () => {
    startSession(signedIn);
    server.use(http.post(`${BFF}/refresh`, () => HttpResponse.error()));

    const error = (await refreshSession().catch((e: unknown) => e)) as ApiError;

    expect(error.code).toBe("NETWORK_ERROR");
    expect(sessionStore.getState().status).toBe("signedIn");
  });

  it("signs out only when the refresh token is refused (401)", async () => {
    startSession(signedIn);
    // The default handler answers 401 REFRESH_TOKEN_INVALID.
    await expect(refreshSession()).resolves.toBe(false);
    expect(sessionStore.getState().status).toBe("signedOut");
    expect(sessionStore.getAccessToken()).toBeNull();
  });

  it("an expired token whose refresh hits a 503 fails that request but keeps the session", async () => {
    startSession(signedIn);
    server.use(
      http.get(`${API}/me`, () =>
        HttpResponse.json(problem(401, "TOKEN_EXPIRED"), { status: 401 }),
      ),
      http.post(`${BFF}/refresh`, () =>
        HttpResponse.json(problem(503, "SERVICE_UNAVAILABLE"), { status: 503 }),
      ),
    );

    const error = (await browserHttp()
      .get("/me")
      .catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(503);
    expect(error.isRetryable).toBe(true);
    expect(sessionStore.getState().status).toBe("signedIn");
  });

  it("on page load an unreachable shop leaves the session unavailable, and a retry restores it", async () => {
    document.cookie = `${SESSION_HINT_COOKIE}=1; Path=/`;
    let calls = 0;
    server.use(
      http.post(`${BFF}/refresh`, () => {
        calls += 1;
        return calls === 1
          ? HttpResponse.json(problem(503, "SERVICE_UNAVAILABLE"), { status: 503 })
          : HttpResponse.json(refreshed);
      }),
    );

    await bootstrapSession();
    expect(sessionStore.getState().status).toBe("unavailable");

    await retrySession();
    expect(sessionStore.getState().status).toBe("signedIn");
    expect(sessionStore.getAccessToken()).toBe("access-2");
  });
});
