// Reads (never verifies) the staff access token's claims. The API verifies the signature; the admin only needs
// the claims to decide what to render. Claim names: backend SharedKernel.Web/Auth/FreshOrbitClaimTypes.cs.

export interface StaffClaims {
  userId: string;
  staffMemberId: string | null;
  actorType: string | null;
  permissions: ReadonlySet<string>;
  /** Location ids from `loc` (empty when `all_loc`). */
  locationIds: readonly string[];
  /** `all_loc: "true"` — may act at every fulfillment location. */
  allLocations: boolean;
  /** Expiry in epoch milliseconds, or null when the token has no `exp`. */
  expiresAt: number | null;
}

function base64UrlDecode(segment: string): string {
  const padded = segment.replace(/-/g, "+").replace(/_/g, "/");
  const fill = padded.length % 4 === 0 ? "" : "=".repeat(4 - (padded.length % 4));
  const binary = atob(padded + fill);
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

/** A repeated claim is an array in JSON; a single one is a plain string. */
function asList(value: unknown): string[] {
  if (Array.isArray(value)) return value.filter((v): v is string => typeof v === "string");
  return typeof value === "string" && value.length > 0 ? [value] : [];
}

/** Decode the payload of a JWT. Returns null for anything that is not a readable JWT. */
export function decodeStaffClaims(token: string | null | undefined): StaffClaims | null {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length < 2 || !parts[1]) return null;
  let payload: Record<string, unknown>;
  try {
    payload = JSON.parse(base64UrlDecode(parts[1])) as Record<string, unknown>;
  } catch {
    return null;
  }
  const sub = typeof payload.sub === "string" ? payload.sub : null;
  if (!sub) return null;
  const allLoc = payload.all_loc;
  return {
    userId: sub,
    staffMemberId: typeof payload.staff_id === "string" ? payload.staff_id : null,
    actorType: typeof payload.actor_type === "string" ? payload.actor_type : null,
    permissions: new Set(asList(payload.perm)),
    locationIds: asList(payload.loc),
    allLocations: allLoc === true || allLoc === "true",
    expiresAt: typeof payload.exp === "number" ? payload.exp * 1000 : null,
  };
}
