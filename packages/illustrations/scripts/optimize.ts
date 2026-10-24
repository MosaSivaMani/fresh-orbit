// Converts final PNG images (from ChatGPT or Gemini) into WebP the apps can ship:
//   - crops to the slot's aspect (1:1 or 4:3), keeping the most interesting part of the picture;
//   - scales the longest side down to 1536 px (never up);
//   - encodes WebP (transparency kept), lowering quality until the file is at most 400 KB;
//   - points the slot's `file` at the WebP and moves the PNG to originals/ (not committed).
//   pnpm --filter @fresh/illustrations optimize
//
// Uses sharp (Apache-2.0) from its prebuilt npm binary; nothing is installed on the system. Without sharp
// the script stops and the PNG stays as it is (validate accepts PNG up to 400 KB).
import { mkdirSync, renameSync, statSync } from "node:fs";
import type SharpModule from "sharp";
import { join } from "node:path";
import { LIMITS, fileStem } from "../src/manifest.ts";
import { packageRoot, readManifest, writeManifest } from "./lib.ts";

type Sharp = typeof SharpModule;
let sharp: Sharp;
try {
  sharp = (await import("sharp")).default;
} catch {
  console.warn("illustrations: sharp is not available, so PNG files are kept as they are.");
  process.exit(0);
}

const MAX_SIDE = 1536;
const QUALITIES = [84, 78, 72, 66, 60];

const manifest = readManifest();
const originals = join(packageRoot, "originals");
let converted = 0;

for (const entry of manifest.slots) {
  if (!entry.file.endsWith(".png")) continue;
  const input = join(packageRoot, entry.file);
  const meta = await sharp(input).metadata();
  if (!meta.width || !meta.height) throw new Error(`${entry.slot}: cannot read ${entry.file}.`);

  const ratio = entry.aspect === "1:1" ? 1 : 4 / 3;
  // Largest crop with the slot's ratio, then at most MAX_SIDE on the long side.
  let width = Math.min(meta.width, Math.round(meta.height * ratio));
  let height = Math.round(width / ratio);
  const scale = Math.min(1, MAX_SIDE / Math.max(width, height));
  width = Math.round(width * scale);
  height = Math.round(height * scale);

  const output = `assets/${fileStem(entry.slot)}.webp`;
  let bytes = Number.POSITIVE_INFINITY;
  for (const quality of QUALITIES) {
    await sharp(input)
      .resize({ width, height, fit: "cover", position: sharp.strategy.attention })
      .webp({ quality, alphaQuality: 90, effort: 6 })
      .toFile(join(packageRoot, output));
    bytes = statSync(join(packageRoot, output)).size;
    if (bytes <= LIMITS.finalMaxBytes) break;
  }

  mkdirSync(originals, { recursive: true });
  renameSync(input, join(originals, `${fileStem(entry.slot)}.png`));
  entry.file = output;
  converted += 1;
  console.log(
    `illustrations: ${entry.slot} -> ${output} (${width}x${height}, ${Math.round(bytes / 1024)} KB)`,
  );
}

writeManifest(manifest);
console.log(
  `illustrations: ${converted} PNG file(s) converted. Run validate next; originals are in originals/.`,
);
