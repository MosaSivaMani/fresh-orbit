// Downloads the OpenAPI document from a running FreshOrbit.Api (Development environment maps /openapi/v1.json)
// and writes it to packages/api-client/openapi.json, pretty-printed for readable diffs.
//
//   pnpm --filter @fresh/api-client fetch-spec [url]
//   default url: http://localhost:5080/openapi/v1.json
import { writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const url = process.argv[2] ?? process.env.OPENAPI_URL ?? "http://localhost:5080/openapi/v1.json";
const out = join(dirname(fileURLToPath(import.meta.url)), "..", "openapi.json");

const response = await fetch(url);
if (!response.ok) {
  console.error(`Could not fetch ${url}: HTTP ${response.status}`);
  process.exit(1);
}
const document = (await response.json()) as { servers?: unknown };
// The server list reflects whichever port the API ran on; it is not part of the contract.
delete document.servers;
writeFileSync(out, `${JSON.stringify(document, null, 2)}\n`);
console.log(`Wrote ${out}`);
