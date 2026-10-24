// Checks manifest.json and the image files. Exits 1 with a list of problems.
//   pnpm --filter @fresh/illustrations validate
import { checkManifest } from "../src/manifest.ts";
import { diskFacts, readManifest } from "./lib.ts";

const manifest = readManifest();
const problems = checkManifest(manifest, diskFacts);
const finals = manifest.slots.filter((s) => s.status === "FINAL").length;

if (problems.length > 0) {
  console.error(`illustrations: ${problems.length} problem(s):`);
  for (const p of problems) console.error(`  - ${p}`);
  process.exit(1);
}
console.log(
  `illustrations: ${manifest.slots.length} slots valid (${finals} FINAL, ${manifest.slots.length - finals} PLACEHOLDER).`,
);
