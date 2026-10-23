import { describe, expect, it } from "vitest";
import { ApiError } from "../http/api-error";
import { createQueryClient, retryDelay, shouldRetry } from "./query-client";

describe("createQueryClient", () => {
  it("applies defaults and lets callers override them", () => {
    const client = createQueryClient({ defaultOptions: { queries: { staleTime: 5 } } });
    const defaults = client.getDefaultOptions();
    expect(defaults.queries?.staleTime).toBe(5);
    expect(defaults.mutations?.retry).toBe(false);
  });

  it("retries only retryable errors, at most twice", () => {
    const network = new ApiError({ status: 0, code: "NETWORK_ERROR" });
    const validation = new ApiError({ status: 422, code: "VALIDATION_FAILED" });
    expect(shouldRetry(0, network)).toBe(true);
    expect(shouldRetry(2, network)).toBe(false);
    expect(shouldRetry(0, validation)).toBe(false);
    expect(shouldRetry(0, new Error("boom"))).toBe(false);
  });

  it("honours Retry-After", () => {
    expect(
      retryDelay(0, new ApiError({ status: 429, code: "RATE_LIMITED", retryAfterSeconds: 3 })),
    ).toBe(3000);
    expect(retryDelay(1, new Error("x"))).toBe(1000);
  });
});
