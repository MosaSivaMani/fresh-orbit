import { existsSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { readImageSize } from "./image-size.ts";
import {
  type FileFacts,
  type Manifest,
  PROMPT_SUFFIX,
  REQUIRED_SLOTS,
  checkManifest,
  composePrompt,
  toRuntime,
} from "./manifest.ts";
import { renderPlaceholder } from "./placeholder-art.ts";

const root = join(import.meta.dirname, "..");
const manifest = JSON.parse(readFileSync(join(root, "manifest.json"), "utf8")) as Manifest;
const disk: FileFacts = {
  size: (p) => (existsSync(join(root, p)) ? statSync(join(root, p)).size : null),
  head: (p) => readFileSync(join(root, p)).subarray(0, 64),
};

// ---------- WebP / PNG headers ----------
function webpVp8x(width: number, height: number): Uint8Array {
  const b = new Uint8Array(30);
  b.set(
    [..."RIFF"].map((c) => c.charCodeAt(0)),
    0,
  );
  b.set(
    [..."WEBP"].map((c) => c.charCodeAt(0)),
    8,
  );
  b.set(
    [..."VP8X"].map((c) => c.charCodeAt(0)),
    12,
  );
  const w = width - 1;
  const h = height - 1;
  b.set([w & 0xff, (w >> 8) & 0xff, (w >> 16) & 0xff], 24);
  b.set([h & 0xff, (h >> 8) & 0xff, (h >> 16) & 0xff], 27);
  return b;
}

function webpVp8(width: number, height: number): Uint8Array {
  const b = new Uint8Array(30);
  b.set(
    [..."RIFF"].map((c) => c.charCodeAt(0)),
    0,
  );
  b.set(
    [..."WEBP"].map((c) => c.charCodeAt(0)),
    8,
  );
  b.set(
    [..."VP8 "].map((c) => c.charCodeAt(0)),
    12,
  );
  b.set([0x9d, 0x01, 0x2a, width & 0xff, width >> 8, height & 0xff, height >> 8], 23);
  return b;
}

function webpVp8l(width: number, height: number): Uint8Array {
  const b = new Uint8Array(30);
  b.set(
    [..."RIFF"].map((c) => c.charCodeAt(0)),
    0,
  );
  b.set(
    [..."WEBP"].map((c) => c.charCodeAt(0)),
    8,
  );
  b.set(
    [..."VP8L"].map((c) => c.charCodeAt(0)),
    12,
  );
  const bits = (width - 1) | ((height - 1) << 14);
  b.set([0x2f, bits & 0xff, (bits >> 8) & 0xff, (bits >> 16) & 0xff, (bits >> 24) & 0xff], 20);
  return b;
}

describe("readImageSize", () => {
  it("reads extended, lossy and lossless WebP headers", () => {
    expect(readImageSize(webpVp8x(1536, 1152))).toEqual({
      format: "WEBP",
      width: 1536,
      height: 1152,
    });
    expect(readImageSize(webpVp8(1024, 768))).toEqual({ format: "WEBP", width: 1024, height: 768 });
    expect(readImageSize(webpVp8l(1200, 1200))).toEqual({
      format: "WEBP",
      width: 1200,
      height: 1200,
    });
  });

  it("reads a PNG header and refuses anything else", () => {
    const png = new Uint8Array(24);
    png.set([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
    png.set([0, 0, 4, 0, 0, 0, 3, 0], 16);
    expect(readImageSize(png)).toEqual({ format: "PNG", width: 1024, height: 768 });
    expect(
      readImageSize(new TextEncoder().encode("<svg xmlns='http://www.w3.org/2000/svg'/>")),
    ).toBeNull();
  });
});

// ---------- manifest rules ----------
const clone = (): Manifest => structuredClone(manifest);
const codes = (problems: string[]) => problems.map((p) => p.split(":")[0]);

describe("manifest.json", () => {
  it("is valid and lists exactly the required slots", () => {
    expect(checkManifest(manifest, disk)).toEqual([]);
    expect(manifest.slots.map((s) => s.slot).sort()).toEqual([...REQUIRED_SLOTS].sort());
  });

  it("builds every prompt from the scene, the character sheet and the style block", () => {
    for (const entry of manifest.slots) {
      expect(entry.prompt.endsWith(PROMPT_SUFFIX)).toBe(true);
      expect(entry.prompt).toContain(manifest.stylePrompt.replace(/\.$/, ""));
      expect(entry.prompt).toContain(manifest.characters[entry.character]!.role);
      expect(entry.prompt).toBe(composePrompt(manifest, entry));
    }
  });

  it("uses the five named characters and never names a studio", () => {
    const text = JSON.stringify(manifest.characters).toLowerCase();
    expect(text).not.toMatch(/pixar|disney|hoodie/);
    expect(Object.values(manifest.characters).map((c) => c.name)).toEqual([
      "Padma",
      "Ravi",
      "Anil",
      "Ammamma",
      "Chanduva",
    ]);
  });

  it("refuses a missing alt text, a duplicate slot and a stale prompt", () => {
    const m = clone();
    m.slots[0]!.alt = "";
    m.slots[1]!.slot = m.slots[2]!.slot;
    m.slots[3]!.scene = "Something new happens";
    const problems = checkManifest(m, disk);
    expect(problems.some((p) => p.startsWith(`${m.slots[0]!.slot}: alt`))).toBe(true);
    expect(problems.some((p) => p.includes("listed twice"))).toBe(true);
    expect(problems.some((p) => p.startsWith(`${m.slots[3]!.slot}: prompt is out of date`))).toBe(
      true,
    );
  });

  it("refuses a FINAL image that is too heavy, too small or the wrong shape", () => {
    const m = clone();
    const entry = m.slots.find((s) => s.aspect === "1:1")!;
    entry.status = "FINAL";
    entry.file = `assets/${entry.slot.toLowerCase()}.webp`;
    const facts = (size: number, header: Uint8Array): FileFacts => ({
      size: (p) => (p === entry.file ? size : disk.size(p)),
      head: (p) => (p === entry.file ? header : disk.head(p)),
    });
    const ok = checkManifest(m, facts(120 * 1024, webpVp8x(1280, 1280)));
    expect(ok).toEqual([]);

    expect(codes(checkManifest(m, facts(401 * 1024, webpVp8x(1280, 1280))))).toEqual([entry.slot]);
    expect(checkManifest(m, facts(80 * 1024, webpVp8x(800, 800)))[0]).toContain("at least 1024");
    expect(checkManifest(m, facts(80 * 1024, webpVp8x(1280, 960)))[0]).toContain("aspect");
  });

  it("accepts a PNG final named after the slot and refuses any other name", () => {
    const m = clone();
    const entry = m.slots.find((s) => s.aspect === "1:1")!;
    entry.status = "FINAL";
    entry.file = `assets/${entry.slot.toLowerCase()}.png`;
    const png = new Uint8Array(24);
    png.set([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
    png.set([0, 0, 4, 0, 0, 0, 4, 0], 16);
    const facts: FileFacts = {
      size: (p) => (p.endsWith(".png") ? 300 * 1024 : disk.size(p)),
      head: (p) => (p.endsWith(".png") ? png : disk.head(p)),
    };
    expect(checkManifest(m, facts)).toEqual([]);
    entry.file = "assets/Final Image.png";
    expect(checkManifest(m, facts).join("\n")).toContain(`assets/${entry.slot.toLowerCase()}.png`);
  });

  it("refuses a FINAL status that still points at the SVG placeholder", () => {
    const m = clone();
    m.slots[0]!.status = "FINAL";
    expect(checkManifest(m, disk).join("\n")).toContain("must be a .png or .webp");
  });

  it("serves the placeholder until the slot is FINAL", () => {
    const entry = structuredClone(manifest.slots[0]!);
    expect(toRuntime(entry).src).toMatch(/\.svg$/);
    entry.status = "FINAL";
    entry.file = "assets/onboarding_fresh.webp";
    expect(toRuntime(entry)).toMatchObject({ src: "onboarding_fresh.webp", width: 1024 });
  });
});

describe("placeholder art", () => {
  it("draws every slot under 6 KB with no text inside the image", () => {
    for (const entry of manifest.slots) {
      const svg = renderPlaceholder(entry.slot, entry.aspect, entry.background);
      expect(Buffer.byteLength(svg)).toBeLessThan(6 * 1024);
      expect(svg).not.toMatch(/<text|<tspan/);
      expect(readFileSync(join(root, entry.placeholder), "utf8")).toBe(svg);
    }
  });
});
