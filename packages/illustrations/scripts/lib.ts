// File access shared by the package scripts.
import {
  closeSync,
  existsSync,
  openSync,
  readFileSync,
  readSync,
  statSync,
  writeFileSync,
} from "node:fs";
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
    const buffer = new Uint8Array(64);
    const fd = openSync(join(packageRoot, path), "r");
    try {
      const read = readSync(fd, buffer, 0, buffer.length, 0);
      return buffer.subarray(0, read);
    } finally {
      closeSync(fd);
    }
  },
};
