import { ApiError } from "@fresh/core";
import { describe, expect, it } from "vitest";
import { unwrap } from "./index";

describe("unwrap", () => {
  it("returns data for a successful response", async () => {
    await expect(
      unwrap({ data: { ok: true }, response: new Response(null, { status: 200 }) }),
    ).resolves.toEqual({
      ok: true,
    });
  });

  it("throws ApiError built from Problem Details", async () => {
    const response = new Response(null, { status: 422 });
    const error = await unwrap({
      error: { status: 422, code: "PINCODE_NOT_SERVICEABLE", traceId: "00-1-01", errors: {} },
      response,
    }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe("PINCODE_NOT_SERVICEABLE");
    expect((error as ApiError).traceId).toBe("00-1-01");
  });
});
