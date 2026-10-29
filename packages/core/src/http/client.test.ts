import { describe, expect, it, vi } from "vitest";
import { ApiError } from "./api-error";
import { type FetchLike, createRefreshHandler } from "./auth-fetch";
import { buildUrl, createHttpClient } from "./client";
import { createIdempotencyKey, ifMatch, parseETag, toETag } from "./headers";
import { pageCount, parseSortParam, toSortParam } from "./paging";
import { createMemoryTokenStore } from "./token-store";

const BASE = "https://api.test/api/v1";

const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": status >= 400 ? "application/problem+json" : "application/json",
      ...headers,
    },
  });

/** The client always hands fetch a Request; normalise for mocks that read it. */
const asFetch =
  (fn: (req: Request) => Promise<Response>): FetchLike =>
  (input, init) =>
    fn(input instanceof Request && init === undefined ? input : new Request(input, init));

const expired = () => json(401, { status: 401, code: "TOKEN_EXPIRED", title: "Token expired." });

describe("createHttpClient", () => {
  it("sends bearer token, JSON body, If-Match and Idempotency-Key and returns data with version", async () => {
    const fetch = vi.fn(async (req: Request) => {
      expect(req.method).toBe("PUT");
      expect(req.url).toBe(`${BASE}/admin/products/p1`);
      expect(req.headers.get("Authorization")).toBe("Bearer access-1");
      expect(req.headers.get("If-Match")).toBe('"918273"');
      expect(req.headers.get("Idempotency-Key")).toBe("key-1");
      expect(req.headers.get("Content-Type")).toBe("application/json");
      expect(await req.json()).toEqual({ name: "Seer fish" });
      return json(200, { id: "p1" }, { ETag: '"918274"' });
    });
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: asFetch(fetch),
      getAccessToken: () => "access-1",
    });

    const res = await client.request<{ id: string }>("PUT", "/admin/products/p1", {
      body: { name: "Seer fish" },
      ifMatch: "918273",
      idempotencyKey: "key-1",
    });

    expect(res.data).toEqual({ id: "p1" });
    expect(res.version).toBe("918274");
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("maps Problem Details to ApiError with code, field errors and traceId", async () => {
    const problem = {
      type: "https://api.freshorbit.in/problems/validation-failed",
      title: "One or more fields are invalid.",
      status: 422,
      code: "VALIDATION_FAILED",
      traceId: "00-abc-01",
      errors: {
        "quantity.value": [
          { code: "QUANTITY_STEP_INVALID", message: "Quantity must be a multiple of 250 grams." },
        ],
      },
    };
    const client = createHttpClient({ baseUrl: BASE, fetch: async () => json(422, problem) });

    const error = await client
      .post("/cart/items", { quantity: { value: 300, unit: "GRAM" } })
      .catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(422);
    expect(apiError.code).toBe("VALIDATION_FAILED");
    expect(apiError.traceId).toBe("00-abc-01");
    expect(apiError.errors["quantity.value"]?.[0]?.code).toBe("QUANTITY_STEP_INVALID");
    expect(apiError.fieldMessage("quantity.value")).toBe(
      "Quantity must be a multiple of 250 grams.",
    );
    expect(apiError.isRetryable).toBe(false);
  });

  it("keeps Problem Details extension members and Retry-After", async () => {
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () =>
        json(
          429,
          { status: 429, code: "RATE_LIMITED", retryAfterSeconds: 7 },
          { "Retry-After": "7" },
        ),
    });
    const error = (await client.get("/catalog").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("RATE_LIMITED");
    expect(error.retryAfterSeconds).toBe(7);
    expect(error.problem.retryAfterSeconds).toBe(7);
    expect(error.isRetryable).toBe(true);
  });

  it("uses HTTP_<status> when the body is not Problem Details", async () => {
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () =>
        new Response("Bad gateway", { status: 502, headers: { "Content-Type": "text/plain" } }),
    });
    const error = (await client.get("/x").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("HTTP_502");
    expect(error.status).toBe(502);
  });

  it("turns transport failures into NETWORK_ERROR", async () => {
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () => {
        throw new TypeError("fetch failed");
      },
    });
    const error = (await client.get("/x").catch((e: unknown) => e)) as ApiError;
    expect(error.status).toBe(0);
    expect(error.code).toBe("NETWORK_ERROR");
    expect(error.isRetryable).toBe(true);
  });

  it("treats no response and every 5xx as an unknown outcome, and a 4xx refusal as known (W-4)", () => {
    const unknown = [
      new ApiError({ status: 0, code: "NETWORK_ERROR" }),
      new ApiError({ status: 500, code: "INTERNAL_ERROR" }),
      new ApiError({ status: 502, code: "HTTP_502" }),
      new ApiError({ status: 504, code: "HTTP_504" }),
      new ApiError({ status: 409, code: "IDEMPOTENCY_IN_PROGRESS" }),
    ];
    for (const error of unknown) expect(error.isOutcomeUnknown).toBe(true);
    const known = [
      new ApiError({ status: 409, code: "PRICE_CHANGED" }),
      new ApiError({ status: 422, code: "VALIDATION_FAILED" }),
      new ApiError({ status: 404, code: "NOT_FOUND" }),
    ];
    for (const error of known) expect(error.isOutcomeUnknown).toBe(false);
  });

  it("returns null data for 204", async () => {
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () => new Response(null, { status: 204 }),
    });
    await expect(client.delete("/wishlist/v1")).resolves.toBeNull();
  });
});

describe("refresh on 401", () => {
  it("refreshes once for concurrent TOKEN_EXPIRED responses and retries each request", async () => {
    let token = "old";
    const refresh = vi.fn(async () => {
      await new Promise((r) => setTimeout(r, 10));
      token = "new";
      return true;
    });
    const fetch = vi.fn(async (req: Request) =>
      req.headers.get("Authorization") === "Bearer new" ? json(200, { url: req.url }) : expired(),
    );
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: asFetch(fetch),
      getAccessToken: () => token,
      refresh,
    });

    const results = await Promise.all([client.get("/a"), client.get("/b"), client.get("/c")]);

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(results).toEqual([{ url: `${BASE}/a` }, { url: `${BASE}/b` }, { url: `${BASE}/c` }]);
    expect(fetch).toHaveBeenCalledTimes(6);
  });

  it("replays the request body on retry", async () => {
    let token = "old";
    const bodies: unknown[] = [];
    const fetch = vi.fn(async (req: Request) => {
      bodies.push(await req.json());
      return req.headers.get("Authorization") === "Bearer new"
        ? json(201, { ok: true })
        : expired();
    });
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: asFetch(fetch),
      getAccessToken: () => token,
      refresh: async () => {
        token = "new";
        return true;
      },
    });
    await client.post("/orders", { cartId: "c1" }, { idempotencyKey: "k" });
    expect(bodies).toEqual([{ cartId: "c1" }, { cartId: "c1" }]);
  });

  it("does not refresh for other 401 codes", async () => {
    const refresh = vi.fn(async () => true);
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () => json(401, { status: 401, code: "UNAUTHENTICATED" }),
      getAccessToken: () => "t",
      refresh,
    });
    const error = (await client.get("/me").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("UNAUTHENTICATED");
    expect(refresh).not.toHaveBeenCalled();
  });

  it("calls onAuthFailure and surfaces the 401 when refresh fails", async () => {
    const onAuthFailure = vi.fn();
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () => expired(),
      getAccessToken: () => "t",
      refresh: async () => false,
      onAuthFailure,
    });
    const error = (await client.get("/me").catch((e: unknown) => e)) as ApiError;
    expect(error.code).toBe("TOKEN_EXPIRED");
    expect(onAuthFailure).toHaveBeenCalledTimes(1);
  });

  it("keeps the session and surfaces the refresh's own error when the refresh cannot run (W-6)", async () => {
    const onAuthFailure = vi.fn();
    const client = createHttpClient({
      baseUrl: BASE,
      fetch: async () => expired(),
      getAccessToken: () => "t",
      refresh: async () => {
        throw new ApiError({ status: 503, code: "SERVICE_UNAVAILABLE", retryAfterSeconds: 5 });
      },
      onAuthFailure,
    });
    const error = (await client.get("/me").catch((e: unknown) => e)) as ApiError;
    expect(error.status).toBe(503);
    expect(error.code).toBe("SERVICE_UNAVAILABLE");
    expect(error.isRetryable).toBe(true);
    expect(onAuthFailure).not.toHaveBeenCalled();
  });

  it("createRefreshHandler keeps the stored tokens and throws on 429 and 5xx", async () => {
    for (const status of [429, 500, 503]) {
      const store = createMemoryTokenStore({ accessToken: "a1", refreshToken: "r1" });
      const handler = createRefreshHandler({
        baseUrl: BASE,
        tokenStore: store,
        fetch: async () => json(status, { status, code: `CODE_${status}` }, { "Retry-After": "3" }),
      });
      const error = (await handler().catch((e: unknown) => e)) as ApiError;
      expect(error).toBeInstanceOf(ApiError);
      expect(error.status).toBe(status);
      expect(store.getRefreshToken()).toBe("r1");
    }
  });

  it("createRefreshHandler rotates tokens in the store and clears it on refusal", async () => {
    const store = createMemoryTokenStore({ accessToken: "a1", refreshToken: "r1" });
    const ok = createRefreshHandler({
      baseUrl: BASE,
      tokenStore: store,
      fetch: async (input, init) => {
        expect(String(input)).toBe(`${BASE}/auth/refresh`);
        expect(JSON.parse(String(init?.body))).toEqual({ refreshToken: "r1" });
        return json(200, { accessToken: "a2", refreshToken: "r2" });
      },
    });
    await expect(ok()).resolves.toBe(true);
    expect(store.getAccessToken()).toBe("a2");
    expect(store.getRefreshToken()).toBe("r2");

    const refused = createRefreshHandler({
      baseUrl: BASE,
      tokenStore: store,
      fetch: async () => json(401, { code: "REFRESH_TOKEN_REUSED" }),
    });
    await expect(refused()).resolves.toBe(false);
    expect(store.getAccessToken()).toBeNull();
  });
});

describe("helpers", () => {
  it("builds URLs with repeated array params and drops empty values", () => {
    expect(
      buildUrl(`${BASE}/`, "admin/orders", {
        status: ["CONFIRMED", "PREPARING"],
        q: undefined,
        page: 2,
      }),
    ).toBe(`${BASE}/admin/orders?status=CONFIRMED&status=PREPARING&page=2`);
  });

  it("quotes and parses ETags", () => {
    expect(toETag("918273")).toBe('"918273"');
    expect(toETag('"918273"')).toBe('"918273"');
    expect(ifMatch(5)).toEqual({ "If-Match": '"5"' });
    expect(parseETag('W/"918273"')).toBe("918273");
    expect(parseETag(null)).toBeUndefined();
  });

  it("keeps one idempotency key per action until reset", () => {
    const key = createIdempotencyKey();
    const first = key.current();
    expect(key.current()).toBe(first);
    expect(first).toMatch(/^[0-9a-f-]{36}$/);
    key.reset();
    expect(key.current()).not.toBe(first);
  });

  it("converts sort params and page counts", () => {
    expect(
      toSortParam([
        { id: "placedAt", desc: true },
        { id: "orderNumber", desc: false },
      ]),
    ).toBe("-placedAt,orderNumber");
    expect(parseSortParam("-placedAt,orderNumber")).toEqual([
      { id: "placedAt", desc: true },
      { id: "orderNumber", desc: false },
    ]);
    expect(pageCount({ pageSize: 20, totalCount: 134 })).toBe(7);
    expect(pageCount({ pageSize: 20, totalCount: 0 })).toBe(1);
  });
});
