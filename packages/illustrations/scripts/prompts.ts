// Rebuilds every slot's `prompt` from its scene, the character sheet and the shared style block.
//   pnpm --filter @fresh/illustrations prompts
import { composePrompt } from "../src/manifest.ts";
import { readManifest, writeManifest } from "./lib.ts";

const manifest = readManifest();
let changed = 0;
for (const entry of manifest.slots) {
  const prompt = composePrompt(manifest, entry);
  if (entry.prompt !== prompt) {
    entry.prompt = prompt;
    changed += 1;
  }
}
writeManifest(manifest);
console.log(`illustrations: ${changed} of ${manifest.slots.length} prompts rebuilt.`);
