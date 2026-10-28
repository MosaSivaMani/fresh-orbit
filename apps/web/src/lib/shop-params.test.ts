import { describe, expect, it } from "vitest";
import { parseShopFilters, toListParams } from "./shop-params";

describe("preparation filter (W8)", () => {
  it("reads server codes from ?prep= and sends them as preparation", () => {
    const filters = parseShopFilters({ prep: ["CURRY_CUT", "STEAK", "CURRY_CUT"] });
    expect(filters.preparations).toEqual(["CURRY_CUT", "STEAK"]);
    expect(toListParams(filters, false).preparation).toEqual(["CURRY_CUT", "STEAK"]);
  });

  it("ignores values that are not codes", () => {
    const filters = parseShopFilters({ prep: ["curry cut", "<script>", ""] });
    expect(filters.preparations).toEqual([]);
    expect(toListParams(filters, true)).not.toHaveProperty("preparation");
  });

  it("keeps a single value as a one-item list", () => {
    expect(parseShopFilters({ prep: "FILLET" }).preparations).toEqual(["FILLET"]);
  });
});
