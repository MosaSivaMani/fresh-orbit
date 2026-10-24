// Brings the owner's final images into the package and marks their slots FINAL.
//   pnpm --filter @fresh/illustrations import
//   pnpm --filter @fresh/illustrations import -- --from <folder from repo root>
//
// Reads <folder>/<slot_lower_snake>.jpg|.png|.webp (default docs/design/characters/generated), and for each:
//   - copies it to assets/<slot_lower_snake>.<ext> (removing an older final of another type);
//   - sets file, status FINAL, width and height (read from the file header), and the source it came from;
//   - sets background from the file: transparency -> TRANSPARENT, otherwise SCENE (a full picture), and
//     rebuilds the prompt when that changes it;
//   - lists any rule the image still breaks (too heavy, wrong shape); `optimize` fixes those.
// A source that was already imported (same path and byte size) is skipped, so running it twice is safe
// and an optimized WebP is not replaced by its original again.
import { copyFileSync, existsSync, readFileSync, readdirSync, rmSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { readImageSize } from "../src/image-size.ts";
import {
  FINAL_FORMATS,
  type FinalExtension,
  checkManifest,
  composePrompt,
  fileStem,
} from "../src/manifest.ts";
import { diskFacts, packageRoot, readManifest, repoRoot, writeManifest } from "./lib.ts";

const fromFlag = process.argv.indexOf("--from");
const fromDir =
  fromFlag >= 0 && process.argv[fromFlag + 1]
    ? process.argv[fromFlag + 1]!
    : "docs/design/characters/generated";
const sourceDir = join(repoRoot, fromDir);
if (!existsSync(sourceDir)) {
  console.error(`illustrations: ${fromDir} does not exist; pass --from <folder>.`);
  process.exit(1);
}

const manifest = readManifest();
const bySlotStem = new Map(manifest.slots.map((s) => [fileStem(s.slot), s]));
const seen = new Map<string, string>();
const imported: string[] = [];
const errors: string[] = [];
let skipped = 0;

for (const name of readdirSync(sourceDir).sort()) {
  const match = /^([a-z0-9_]+)\.(jpe?g|png|webp)$/.exec(name);
  if (!match) {
    if (!name.startsWith("."))
      errors.push(`${name}: not a .jpg, .png or .webp named after a slot.`);
    continue;
  }
  const stem = match[1]!;
  const ext = (match[2] === "jpeg" ? "jpg" : match[2]) as FinalExtension;
  const entry = bySlotStem.get(stem);
  if (!entry) {
    errors.push(`${name}: no slot is called ${stem.toUpperCase()}; check the file name.`);
    continue;
  }
  if (seen.has(stem)) {
    errors.push(`${name}: ${seen.get(stem)} is also there for the same slot; keep one.`);
    continue;
  }
  seen.set(stem, name);

  const sourcePath = join(sourceDir, name);
  const source = relative(repoRoot, sourcePath);
  const sourceBytes = statSync(sourcePath).size;
  if (
    entry.status === "FINAL" &&
    entry.source === source &&
    entry.sourceBytes === sourceBytes &&
    diskFacts.size(entry.file) !== null
  ) {
    skipped += 1;
    continue;
  }

  const bytes = readFileSync(sourcePath);
  const dims = readImageSize(bytes);
  if (!dims || dims.format !== FINAL_FORMATS[ext]) {
    errors.push(`${name}: is not a readable ${FINAL_FORMATS[ext]} image; export it again.`);
    continue;
  }

  const file = `assets/${stem}.${ext}`;
  for (const other of Object.keys(FINAL_FORMATS)) {
    const old = join(packageRoot, `assets/${stem}.${other}`);
    if (other !== ext && existsSync(old)) rmSync(old);
  }
  copyFileSync(sourcePath, join(packageRoot, file));

  const background = dims.alpha ? "TRANSPARENT" : "SCENE";
  const backgroundChanged = entry.background !== background;
  Object.assign(entry, {
    file,
    status: "FINAL",
    width: dims.width,
    height: dims.height,
    background,
    source,
    sourceBytes,
  });
  if (backgroundChanged) entry.prompt = composePrompt(manifest, entry);
  imported.push(entry.slot);
  console.log(
    `illustrations: ${entry.slot} <- ${source} (${dims.width}x${dims.height}, ${Math.round(sourceBytes / 1024)} KB, ${background})`,
  );
}

writeManifest(manifest);

const problems = checkManifest(manifest, diskFacts).filter((p) =>
  imported.some((slot) => p.startsWith(`${slot}:`)),
);
console.log(`illustrations: imported ${imported.length}, unchanged ${skipped}, from ${fromDir}.`);
for (const e of errors) console.error(`  - ${e}`);
if (problems.length > 0) {
  console.error(
    "illustrations: some imported images break a rule; run `pnpm --filter @fresh/illustrations optimize`, then validate:",
  );
  for (const p of problems) console.error(`  - ${p}`);
}
if (errors.length > 0 || problems.length > 0) process.exit(1);
