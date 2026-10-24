import { describe, expect, it } from "vitest";
import tokens from "../tokens.json" with { type: "json" };
import { AA_NON_TEXT, AA_TEXT, contrastRatio } from "./contrast.ts";

type Role = keyof typeof tokens.color.light;
const themes = ["light", "dark"] as const;

describe("contrastRatio", () => {
  it("matches known WCAG values", () => {
    expect(contrastRatio("#000000", "#FFFFFF")).toBeCloseTo(21, 5);
    expect(contrastRatio("#FFFFFF", "#FFFFFF")).toBeCloseTo(1, 5);
    expect(contrastRatio("#777777", "#FFFFFF")).toBeCloseTo(4.48, 2);
  });

  it("rejects colours that are not #RRGGBB", () => {
    expect(() => contrastRatio("teal", "#FFFFFF")).toThrow();
  });
});

describe("design token contrast (WCAG AA)", () => {
  it("both themes define the same roles", () => {
    expect(Object.keys(tokens.color.dark).sort()).toEqual(Object.keys(tokens.color.light).sort());
  });

  for (const theme of themes) {
    const palette = tokens.color[theme] as Record<Role, string>;

    for (const [fg, bg] of tokens.contrast.text as [Role, Role][]) {
      it(`${theme}: ${fg} on ${bg} is at least ${AA_TEXT}:1`, () => {
        expect(contrastRatio(palette[fg], palette[bg])).toBeGreaterThanOrEqual(AA_TEXT);
      });
    }

    for (const [fg, bg] of tokens.contrast.ui as [Role, Role][]) {
      it(`${theme}: ${fg} against ${bg} is at least ${AA_NON_TEXT}:1 (UI component)`, () => {
        expect(contrastRatio(palette[fg], palette[bg])).toBeGreaterThanOrEqual(AA_NON_TEXT);
      });
    }
  }

  // D53 illustrated look: coral is decorative; anything with text uses accentCoralText or onAccentCoral.
  // accentCoralText is not used on surfaceSandStrong (light 4.2:1).
  for (const theme of themes) {
    const palette = tokens.color[theme] as Record<Role, string>;
    for (const bg of ["surface", "background", "surfaceSand", "surfacePaper"] as const) {
      it(`${theme}: accentCoralText is readable on ${bg}`, () => {
        expect(contrastRatio(palette.accentCoralText, palette[bg])).toBeGreaterThanOrEqual(AA_TEXT);
      });
    }
    it(`${theme}: sticker label text is readable on coral and mango`, () => {
      expect(contrastRatio(palette.onMango, palette.mango)).toBeGreaterThanOrEqual(AA_TEXT);
      expect(contrastRatio(palette.onAccentCoral, palette.accentCoral)).toBeGreaterThanOrEqual(
        AA_TEXT,
      );
    });
  }

  it("checks every text-bearing illustrated role in the pair list", () => {
    const pairs = tokens.contrast.text.map(([fg, bg]) => `${fg}/${bg}`);
    expect(pairs).toEqual(
      expect.arrayContaining([
        "accentCoralText/surface",
        "accentCoralText/surfaceSand",
        "onAccentCoral/accentCoral",
        "text/surfaceSand",
        "textMuted/surfaceSand",
      ]),
    );
  });

  it("keeps the four brand colours exactly", () => {
    expect(tokens.brand).toEqual({
      oceanTeal: "#073B4C",
      seaGreen: "#0E9384",
      softBackground: "#F4F8F7",
      warmSand: "#E9AD55",
    });
    expect(tokens.color.light.primary).toBe(tokens.brand.oceanTeal);
    expect(tokens.color.light.background).toBe(tokens.brand.softBackground);
    expect(tokens.color.light.highlight).toBe(tokens.brand.warmSand);
    expect(tokens.color.light.ring).toBe(tokens.brand.seaGreen);
  });
});
