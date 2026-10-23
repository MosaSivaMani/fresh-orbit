// Same-origin proxy from the browser to FreshOrbit.Api (`/api/v1/*` on this site → `${FRESH_API_ORIGIN}/api/v1/*`).
//
// Why a proxy and not CORS: the guest cart token stays in an httpOnly cookie (scripts never see it), the visitor's
// pincode cookie is added as `X-Pincode`, and the API needs no CORS policy for the website. Authorization is passed
// through unchanged (the access token lives in browser memory). Endpoints that hand out refresh tokens, admin
// endpoints, webhooks and development-only endpoints are not reachable through it.
import { CART_COOKIE, GUEST_CART_SECONDS, PINCODE_COOKIE, isValidPincode } from "@/lib/cookies";
import { ConfigurationMissingError, getApiOrigin, isProduction } from "./config";
import { problemResponse } from "./problem";

const BLOCKED_PATHS = [
  /^admin(\/|$)/,
  /^auth\/(otp\/verify|refresh|logout)$/,
  /^payments\/webhooks(\/|$)/,
  /^dev(\/|$)/,
];

const REQUEST_HEADERS = [
  "accept",
  "content-type",
  "authorization",
  "if-match",
  "if-none-match",
  "idempotency-key",
  "x-correlation-id",
  "x-pincode",
];

const RESPONSE_HEADERS = [
  "content-type",
  "etag",
  "location",
  "retry-after",
  "x-correlation-id",
  "idempotent-replayed",
  "x-fulfillment-location",
  "x-service-area",
  "content-disposition",
];

export interface ProxyCookies {
  get(name: string): { value: string } | undefined;
}

export interface ProxyResult {
  response: Response;
  /** New guest cart token issued by the API; the caller stores it in the httpOnly cookie. */
  issuedCartToken: string | null;
}

/** Validates and joins the catch-all segments; rejects traversal and empty segments. */
export function toApiPath(segments: readonly string[]): string | null {
  if (segments.length === 0) return null;
  for (const segment of segments) {
    if (segment === "" || segment === "." || segment === "..") return null;
  }
  return segments.map((s) => encodeURIComponent(s)).join("/");
}

export async function proxyToApi(
  request: Request,
  segments: readonly string[],
  cookies: ProxyCookies,
  fetchImpl: typeof fetch = fetch,
): Promise<ProxyResult> {
  const path = toApiPath(segments);
  if (path === null || BLOCKED_PATHS.some((pattern) => pattern.test(path))) {
    return {
      response: problemResponse(404, "NOT_FOUND", "Resource not found."),
      issuedCartToken: null,
    };
  }

  let origin: string;
  try {
    origin = getApiOrigin();
  } catch (error) {
    if (error instanceof ConfigurationMissingError) {
      return {
        response: problemResponse(503, "SERVICE_UNAVAILABLE", "The shop is not configured."),
        issuedCartToken: null,
      };
    }
    throw error;
  }

  const incoming = new URL(request.url);
  const target = `${origin}/api/v1/${path}${incoming.search}`;

  const headers = new Headers();
  for (const name of REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value !== null) headers.set(name, value);
  }
  if (!headers.has("x-pincode")) {
    const pincode = cookies.get(PINCODE_COOKIE)?.value;
    if (isValidPincode(pincode)) headers.set("X-Pincode", pincode);
  }
  const cartToken = cookies.get(CART_COOKIE)?.value;
  if (cartToken) headers.set("X-Cart-Token", cartToken);
  const forwardedFor = request.headers.get("x-forwarded-for") ?? request.headers.get("x-real-ip");
  if (forwardedFor) headers.set("X-Forwarded-For", forwardedFor);

  const method = request.method.toUpperCase();
  const body = method === "GET" || method === "HEAD" ? undefined : await request.arrayBuffer();

  let upstream: Response;
  try {
    upstream = await fetchImpl(target, {
      method,
      headers,
      ...(body !== undefined && body.byteLength > 0 ? { body } : {}),
      cache: "no-store",
      redirect: "manual",
    });
  } catch {
    return {
      response: problemResponse(503, "SERVICE_UNAVAILABLE", "The shop is not reachable right now."),
      issuedCartToken: null,
    };
  }

  const outHeaders = new Headers({ "Cache-Control": "no-store" });
  for (const name of RESPONSE_HEADERS) {
    const value = upstream.headers.get(name);
    if (value !== null) outHeaders.set(name, value);
  }

  const issuedCartToken = upstream.headers.get("x-cart-token");
  const isJson = (upstream.headers.get("content-type") ?? "").includes("json");
  const noBody = upstream.status === 204 || upstream.status === 304 || method === "HEAD";

  let responseBody: BodyInit | null = noBody ? null : upstream.body;
  if (!noBody && isJson && path.startsWith("cart")) {
    // The guest token is returned once in the body too (`cartToken`); keep it out of reach of scripts.
    const text = await upstream.text();
    try {
      const data = JSON.parse(text) as unknown;
      if (data !== null && typeof data === "object" && "cartToken" in data) {
        (data as { cartToken: unknown }).cartToken = null;
      }
      responseBody = JSON.stringify(data);
    } catch {
      responseBody = text;
    }
  }

  return {
    response: new Response(responseBody, { status: upstream.status, headers: outHeaders }),
    issuedCartToken,
  };
}

export const guestCartCookieOptions = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: isProduction,
  path: "/",
  maxAge: GUEST_CART_SECONDS,
};
