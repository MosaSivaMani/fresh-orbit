// HTTP clients for Server Components and metadata routes. They call FreshOrbit.Api directly (server to server) and
// only ever make public calls: the customer's access token lives in browser memory, so signed-in data is fetched by
// Client Components through the /api/v1 proxy.
import { createHttpClient, type FetchLike, type HttpClient, isApiError } from "@fresh/core";
import { cookies, headers } from "next/headers";
import { PINCODE_COOKIE, isValidPincode } from "@/lib/cookies";
import { getApiOrigin } from "@/server/config";

function client(fetchImpl: FetchLike, extraHeaders: Record<string, string>): HttpClient {
  return createHttpClient({
    baseUrl: `${getApiOrigin()}/api/v1`,
    fetch: fetchImpl,
    defaultHeaders: () => extraHeaders,
  });
}

const noStore: FetchLike = (input, init) => fetch(input, { ...init, cache: "no-store" });

export interface RequestApi {
  /** Sends the visitor's pincode (`X-Pincode`) when one is set. */
  http: HttpClient;
  /** Same, without a pincode (prices hidden, `locationRequired: true`). */
  anonymous: HttpClient;
  pincode: string | null;
}

/**
 * Per-request clients. They forward the visitor's IP (`X-Forwarded-For`, so the API's per-IP rate limits see the
 * shopper, not this server). Reading cookies makes the page dynamic (rendered per request), which is required
 * because prices and availability depend on the pincode.
 */
export async function requestApi(): Promise<RequestApi> {
  const jar = await cookies();
  const incoming = await headers();
  const raw = jar.get(PINCODE_COOKIE)?.value;
  const pincode = isValidPincode(raw) ? raw : null;
  const base: Record<string, string> = {};
  const forwardedFor = incoming.get("x-forwarded-for") ?? incoming.get("x-real-ip");
  if (forwardedFor) base["X-Forwarded-For"] = forwardedFor;
  const anonymous = client(noStore, base);
  const http = pincode ? client(noStore, { ...base, "X-Pincode": pincode }) : anonymous;
  return { http, anonymous, pincode };
}

export interface Located<T> {
  data: T;
  pincode: string | null;
  /** The stored pincode is no longer served (area or city deactivated); data was loaded without it. */
  pincodeRefused: boolean;
}

/**
 * Loads location-dependent data with the visitor's pincode. When the API refuses the pincode (422
 * `PINCODE_NOT_SERVICEABLE` / `CITY_NOT_ACTIVE`), loads it without one and says so, so the page can tell the
 * visitor instead of failing.
 */
export async function loadWithLocation<T>(
  api: RequestApi,
  load: (http: HttpClient) => Promise<T>,
): Promise<Located<T>> {
  try {
    return { data: await load(api.http), pincode: api.pincode, pincodeRefused: false };
  } catch (error) {
    if (
      api.pincode &&
      isApiError(error) &&
      (error.code === "PINCODE_NOT_SERVICEABLE" || error.code === "CITY_NOT_ACTIVE")
    ) {
      return { data: await load(api.anonymous), pincode: api.pincode, pincodeRefused: true };
    }
    throw error;
  }
}

/** Location-free public data (categories, cities) cached for `revalidateSeconds`; safe in static pages. */
export function publicHttp(revalidateSeconds: number): HttpClient {
  const cached: FetchLike = (input, init) =>
    fetch(input, { ...init, next: { revalidate: revalidateSeconds } });
  return client(cached, {});
}
