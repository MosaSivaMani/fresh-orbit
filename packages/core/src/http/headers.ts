// Idempotency-Key and If-Match / ETag helpers (docs/api/README.md §1.7, §1.8).

/**
 * New Idempotency-Key (UUID). Create one per user action and reuse it for every retry of that action,
 * e.g. keep it in component state from "Place order" click until success.
 */
export function newIdempotencyKey(): string {
  return globalThis.crypto.randomUUID();
}

/**
 * Holds one key per logical action: `current()` stays the same across retries until `reset()` is called
 * after the action succeeded (or the user changed the request body).
 */
export function createIdempotencyKey(): { current(): string; reset(): void } {
  let key = newIdempotencyKey();
  return {
    current: () => key,
    reset: () => {
      key = newIdempotencyKey();
    },
  };
}

/** Quote a resource `version` for `If-Match` / `If-None-Match`: `918273` -> `"918273"`. */
export function toETag(version: string | number): string {
  const v = String(version).trim();
  if (v === "*") return v;
  if (v.startsWith('"') || v.startsWith("W/")) return v;
  return `"${v}"`;
}

/** Read the bare version from an `ETag` header value: `W/"918273"` -> `918273`. */
export function parseETag(header: string | null | undefined): string | undefined {
  if (!header) return undefined;
  const v = header.trim().replace(/^W\//, "");
  return v.startsWith('"') && v.endsWith('"') ? v.slice(1, -1) : v;
}

/** Version of a response: the `ETag` header. */
export function versionFromResponse(response: Response): string | undefined {
  return parseETag(response.headers.get("ETag"));
}

/** Header object for an optimistic-concurrency update. */
export function ifMatch(version: string | number): { "If-Match": string } {
  return { "If-Match": toETag(version) };
}

/** Header for create-if-absent (`If-None-Match: *`). */
export const IF_NONE_MATCH_ANY = { "If-None-Match": "*" } as const;
