// File access shared by the package scripts.
import { existsSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { type FileFacts, type Manifest } from "../src/manifest.ts";

export const packageRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
export const repoRoot = join(packageRoot, "..", "..");
export const manifestPath = join(packageRoot, "manifest.json");

export function readManifest(): Manifest {
  return JSON.parse(readFileSync(manifestPath, "utf8")) as Manifest;
}

export function writeManifest(manifest: Manifest): void {
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
}

/** Facts about files, with paths relative to the package root. */
export const diskFacts: FileFacts = {
  size(path) {
    const full = join(packageRoot, path);
    return existsSync(full) ? statSync(full).size : null;
  },
  head(path) {
    // Whole file: JPEG frame headers can sit after large metadata, and finals are at most 600 KB.
    return readFileSync(join(packageRoot, path));
  },
};
