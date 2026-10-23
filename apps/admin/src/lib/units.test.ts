import { describe, expect, it } from "vitest";
import {
  bpToPercentInput,
  istLocalToUtcIso,
  paiseToRupeesInput,
  parsePercentToBp,
  parseRupeesToPaise,
  parseWholeNumber,
  utcIsoToIstLocal,
} from "./units";

describe("input parsing", () => {
  it("parses rupees to paise without floating-point error", () => {
    expect(parseRupeesToPaise("1249.5")).toBe(124950);
    expect(parseRupeesToPaise("0.29")).toBe(29);
    expect(parseRupeesToPaise("1,24,999")).toBe(12499900);
    expect(parseRupeesToPaise("12.345")).toBeNull();
    expect(parseRupeesToPaise("-1")).toBeNull();
    expect(parseRupeesToPaise("abc")).toBeNull();
  });

  it("formats paise for an input field", () => {
    expect(paiseToRupeesInput(124950)).toBe("1249.50");
    expect(paiseToRupeesInput(5)).toBe("0.05");
    expect(paiseToRupeesInput(null)).toBe("");
  });

  it("parses whole numbers and percentages", () => {
    expect(parseWholeNumber(" 540 ")).toBe(540);
    expect(parseWholeNumber("5.5")).toBeNull();
    expect(parsePercentToBp("5")).toBe(500);
    expect(parsePercentToBp("12.5")).toBe(1250);
    expect(bpToPercentInput(1250)).toBe("12.50");
    expect(bpToPercentInput(500)).toBe("5");
  });

  it("converts IST wall-clock input to UTC and back", () => {
    expect(istLocalToUtcIso("2026-10-24T06:00")).toBe("2026-10-24T00:30:00.000Z");
    expect(utcIsoToIstLocal("2026-10-24T00:30:00Z")).toBe("2026-10-24T06:00");
    expect(istLocalToUtcIso("tomorrow")).toBeNull();
  });
});
