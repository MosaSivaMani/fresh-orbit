// Writes every slot's placeholder SVG into assets/ from src/placeholder-art.ts.
//   pnpm --filter @fresh/illustrations placeholders
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { LIMITS } from "../src/manifest.ts";
import { renderPlaceholder } from "../src/placeholder-art.ts";
import { packageRoot, readManifest } from "./lib.ts";

const manifest = readManifest();
let largest = 0;
for (const entry of manifest.slots) {
  const svg = renderPlaceholder(entry.slot, entry.aspect, entry.background);
  const bytes = Buffer.byteLength(svg);
  if (bytes > LIMITS.placeholderMaxBytes)
    throw new Error(`${entry.slot}: placeholder is ${bytes} bytes; simplify it.`);
  largest = Math.max(largest, bytes);
  const out = join(packageRoot, entry.placeholder);
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, svg);
}
console.log(
  `illustrations: wrote ${manifest.slots.length} placeholders (largest ${largest} bytes).`,
);
