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
  head: (p) => readFileSync(join(root, p)),
};

// ---------- WebP / PNG / JPEG headers ----------
function jpeg(width: number, height: number): Uint8Array {
  // SOI, an APP0 segment (16 bytes), then SOF0 with height and width.
  const app0 = [0xff, 0xe0, 0, 16, ...Array<number>(14).fill(0)];
  const sof0 = [0xff, 0xc0, 0, 17, 8, height >> 8, height & 0xff, width >> 8, width & 0xff, 3];
  return new Uint8Array([0xff, 0xd8, ...app0, ...sof0, ...Array<number>(9).fill(0)]);
}

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
      alpha: false,
      width: 1536,
      height: 1152,
    });
    expect(readImageSize(webpVp8(1024, 768))).toEqual({
      format: "WEBP",
      width: 1024,
      height: 768,
      alpha: false,
    });
    expect(readImageSize(webpVp8l(1200, 1200))).toEqual({
      format: "WEBP",
      alpha: false,
      width: 1200,
      height: 1200,
    });
  });

  it("reads a PNG header and refuses anything else", () => {
    const png = new Uint8Array(26);
    png.set([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
    png.set([0, 0, 4, 0, 0, 0, 3, 0], 16);
    expect(readImageSize(png)).toEqual({ format: "PNG", width: 1024, height: 768, alpha: false });
    png[25] = 6;
    expect(readImageSize(png)?.alpha).toBe(true);
    expect(readImageSize(jpeg(1448, 1086))).toEqual({
      format: "JPEG",
      width: 1448,
      height: 1086,
      alpha: false,
    });
    expect(
      readImageSize(new TextEncoder().encode("<svg xmlns='http://www.w3.org/2000/svg'/>")),
    ).toBeNull();
  });
});

// ---------- manifest rules ----------
const clone = (): Manifest => structuredClone(manifest);
const codes = (problems: string[]) => problems.map((p) => p.split(":")[0]);
/** A slot still on its placeholder, so a test can turn it FINAL. */
const placeholderSlot = (m: Manifest, aspect: "1:1" | "4:3" = "1:1") =>
  m.slots.find((s) => s.status === "PLACEHOLDER" && s.aspect === aspect)!;

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
    const entry = placeholderSlot(m);
    Object.assign(entry, { status: "FINAL", width: 1280, height: 1280, background: "SCENE" });
    entry.prompt = composePrompt(m, entry);
    entry.file = `assets/${entry.slot.toLowerCase()}.webp`;
    const facts = (size: number, header: Uint8Array): FileFacts => ({
      size: (p) => (p === entry.file ? size : disk.size(p)),
      head: (p) => (p === entry.file ? header : disk.head(p)),
    });
    const ok = checkManifest(m, facts(120 * 1024, webpVp8x(1280, 1280)));
    expect(ok).toEqual([]);

    expect(codes(checkManifest(m, facts(601 * 1024, webpVp8x(1280, 1280))))).toEqual([entry.slot]);
    expect(checkManifest(m, facts(599 * 1024, webpVp8x(1280, 1280)))).toEqual([]);
    Object.assign(entry, { width: 800, height: 800 });
    expect(checkManifest(m, facts(80 * 1024, webpVp8x(800, 800)))[0]).toContain("at least 1024");
    Object.assign(entry, { width: 1280, height: 960 });
    expect(checkManifest(m, facts(80 * 1024, webpVp8x(1280, 960)))[0]).toContain("aspect");
    expect(checkManifest(m, facts(80 * 1024, webpVp8x(1280, 1280))).join("\n")).toContain(
      "width and height must be the file size 1280x1280",
    );
  });

  it("accepts a PNG final named after the slot and refuses any other name", () => {
    const m = clone();
    const entry = placeholderSlot(m);
    Object.assign(entry, { status: "FINAL", width: 1024, height: 1024, background: "SCENE" });
    entry.prompt = composePrompt(m, entry);
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

  it("accepts a JPEG scene final and refuses a JPEG on a TRANSPARENT slot", () => {
    const m = clone();
    const entry = placeholderSlot(m, "4:3");
    Object.assign(entry, { status: "FINAL", width: 1448, height: 1086, background: "SCENE" });
    entry.file = `assets/${entry.slot.toLowerCase()}.jpg`;
    entry.prompt = composePrompt(m, entry);
    const facts: FileFacts = {
      size: (p) => (p.endsWith(entry.file) ? 505 * 1024 : disk.size(p)),
      head: (p) => (p.endsWith(entry.file) ? jpeg(1448, 1086) : disk.head(p)),
    };
    expect(checkManifest(m, facts)).toEqual([]);
    entry.background = "TRANSPARENT";
    entry.prompt = composePrompt(m, entry);
    expect(checkManifest(m, facts).join("\n")).toContain("has no transparency");
  });

  it("refuses a FINAL status that still points at the SVG placeholder", () => {
    const m = clone();
    placeholderSlot(m).status = "FINAL";
    expect(checkManifest(m, disk).join("\n")).toContain("must be a .jpg, .png or .webp");
  });

  it("serves the placeholder until the slot is FINAL", () => {
    const entry = structuredClone(placeholderSlot(manifest, "4:3"));
    expect(toRuntime(entry)).toMatchObject({ width: 1024, height: 768 });
    expect(toRuntime(entry).src).toMatch(/\.svg$/);
    Object.assign(entry, { status: "FINAL", width: 1448, height: 1086 });
    entry.file = `assets/${entry.slot.toLowerCase()}.jpg`;
    expect(toRuntime(entry)).toMatchObject({
      src: `${entry.slot.toLowerCase()}.jpg`,
      width: 1448,
      height: 1086,
    });
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
