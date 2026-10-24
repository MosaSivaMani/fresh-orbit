#!/usr/bin/env node
// Copies the illustration files the manifest uses into a web app's public folder.
//   fresh-illustrations-copy public/illustrations
// Plain JavaScript (not .ts) so it also runs from node_modules, where Node does not strip types.
import { copyFileSync, mkdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const target = process.argv[2];
if (!target) {
  console.error(
    "Usage: fresh-illustrations-copy <target folder>, for example public/illustrations.",
  );
  process.exit(1);
}

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const manifest = JSON.parse(readFileSync(join(root, "manifest.json"), "utf8"));
const out = resolve(process.cwd(), target);
mkdirSync(out, { recursive: true });

const files = new Set();
for (const entry of manifest.slots) {
  files.add(entry.placeholder);
  files.add(entry.file);
}
for (const file of files) {
  copyFileSync(join(root, file), join(out, file.slice(file.lastIndexOf("/") + 1)));
}
console.log(`illustrations: copied ${files.size} files to ${target}.`);
