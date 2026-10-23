import { describe, expect, it } from "vitest";
import { fakeAccessToken } from "../test/render";
import { decodeStaffClaims } from "./jwt";

describe("decodeStaffClaims", () => {
  it("reads permissions and locations from repeated claims", () => {
    const claims = decodeStaffClaims(
      fakeAccessToken({
        permissions: ["orders.view", "orders.pack"],
        locationIds: ["loc-1", "loc-2"],
      }),
    );
    expect(claims?.permissions.has("orders.pack")).toBe(true);
    expect(claims?.permissions.has("orders.cancel")).toBe(false);
    expect(claims?.locationIds).toEqual(["loc-1", "loc-2"]);
    expect(claims?.allLocations).toBe(false);
    expect(claims?.staffMemberId).toBe("0192c000-0000-7000-8000-000000000001");
  });

  it('accepts a single claim value as a string and all_loc as "true"', () => {
    const payload = btoa(JSON.stringify({ sub: "u1", perm: "orders.view", all_loc: "true" }));
    const claims = decodeStaffClaims(`x.${payload}.y`);
    expect([...(claims?.permissions ?? [])]).toEqual(["orders.view"]);
    expect(claims?.allLocations).toBe(true);
    expect(claims?.locationIds).toEqual([]);
  });

  it("returns null for anything that is not a JWT", () => {
    expect(decodeStaffClaims("not-a-token")).toBeNull();
    expect(decodeStaffClaims(null)).toBeNull();
    expect(decodeStaffClaims("a.%%%.c")).toBeNull();
  });
});
