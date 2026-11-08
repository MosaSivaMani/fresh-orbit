import { describe, expect, it } from "vitest";
import { type ServingInfo } from "@/api/types/catalog";
import {
  discountLabel,
  freshnessLine,
  packFacts,
  servingLine,
  variantPackFacts,
} from "./product-facts";

const perKg: ServingInfo = {
  basis: { value: 1000, unit: "GRAM" },
  netWeightGrams: 1000,
  piecesMin: 10,
  piecesMax: 14,
  serves: 4,
};

const pack: ServingInfo = {
  basis: { value: 1, unit: "PACK" },
  netWeightGrams: 500,
  piecesMin: 3,
  piecesMax: 3,
  serves: 2,
};

const piece: ServingInfo = {
  basis: { value: 1, unit: "PIECE" },
  netWeightGrams: null,
  piecesMin: null,
  piecesMax: null,
  serves: 1,
};

describe("servingLine", () => {
  it("states the server's facts per basis, unscaled", () => {
    expect(servingLine(perKg)).toBe("1 kg · 10–14 pcs · serves 4");
  });

  it("uses the pack's net weight and a single piece count", () => {
    expect(servingLine(pack)).toBe("500 g · 3 pcs · serves 2");
  });

  it("falls back to the basis when the net weight is unknown and omits a missing range", () => {
    expect(servingLine(piece)).toBe("1 piece · serves 1");
  });

  it("shows nothing when the server states neither pieces nor serves", () => {
    expect(servingLine({ ...perKg, piecesMin: null, piecesMax: null, serves: null })).toBeNull();
    expect(servingLine(null)).toBeNull();
    expect(servingLine(undefined)).toBeNull();
  });
});

describe("packFacts", () => {
  it("states the pack's net weight and pieces range (D348)", () => {
    expect(packFacts(500, 5, 7)).toBe("500 g net · 5–7 pieces");
  });

  it("writes 1000 g as 1 kg and a fixed count once", () => {
    expect(packFacts(1000, 6, 6)).toBe("1 kg net · 6 pieces");
    expect(packFacts(250, 1, 1)).toBe("250 g net · 1 piece");
  });

  it("leaves the pieces out when the server does not know them", () => {
    expect(packFacts(500, null, null)).toBe("500 g net");
    expect(packFacts(500)).toBe("500 g net");
  });

  it("shows nothing without the net weight", () => {
    expect(packFacts(null, 5, 7)).toBeNull();
    expect(packFacts(undefined)).toBeNull();
  });
});

describe("variantPackFacts", () => {
  it("describes a PACK variant from its pack size and serving pieces", () => {
    expect(
      variantPackFacts({
        sellUnit: "PACK",
        packSizeGrams: 500,
        serving: { ...pack, piecesMin: 5, piecesMax: 7 },
      }),
    ).toBe("500 g net · 5–7 pieces");
  });

  it("falls back to the serving's net weight for a list variant", () => {
    expect(variantPackFacts({ sellUnit: "PACK", serving: pack })).toBe("500 g net · 3 pieces");
  });

  it("is null for a variant sold by weight or piece", () => {
    expect(variantPackFacts({ sellUnit: "KG", packSizeGrams: null, serving: perKg })).toBeNull();
    expect(variantPackFacts({ sellUnit: "PIECE", serving: piece })).toBeNull();
  });
});

describe("discountLabel", () => {
  it("formats the server's basis points", () => {
    expect(discountLabel(1250)).toBe("12.5% off");
    expect(discountLabel(2000)).toBe("20% off");
  });

  it("is empty without a saving", () => {
    expect(discountLabel(null)).toBeUndefined();
    expect(discountLabel(undefined)).toBeUndefined();
    expect(discountLabel(0)).toBeUndefined();
  });
});

describe("freshnessLine", () => {
  it("names the catch date when the server knows it", () => {
    const caught = { caughtOn: "2026-10-23", receivedAt: "2026-10-23T02:10:00Z" };
    expect(freshnessLine(caught)).toBe("Caught 23 Oct 2026");
    expect(freshnessLine({ ...caught, caughtOn: null })).toBeNull();
    expect(freshnessLine(null)).toBeNull();
  });
});
