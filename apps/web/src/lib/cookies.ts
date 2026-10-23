// Cookie names and helpers shared by server and browser code. See docs/design/web.md §4 (auth and cookies).

/** Delivery pincode chosen by the visitor. Readable by scripts (shown in the header); not a secret. */
export const PINCODE_COOKIE = "fo_pincode";
/** Guest cart token (docs/api §1.3). httpOnly; added to API calls by the website's /api/v1 proxy. */
export const CART_COOKIE = "fo_cart";
/** Customer refresh token. httpOnly, SameSite=Strict, path /bff/auth only. */
export const REFRESH_COOKIE = "fo_rt";
/** "1" while a refresh cookie exists, so the browser knows to restore the session. Holds no secret. */
export const SESSION_HINT_COOKIE = "fo_session";

export const PINCODE_PATTERN = /^[1-9][0-9]{5}$/;

export const ONE_YEAR_SECONDS = 365 * 24 * 60 * 60;
/** Guest carts live 7 days after the last write (CART-R20). */
export const GUEST_CART_SECONDS = 7 * 24 * 60 * 60;

export function isValidPincode(value: string | null | undefined): value is string {
  return value != null && PINCODE_PATTERN.test(value);
}

/** Browser only: read a non-httpOnly cookie. */
export function readBrowserCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  for (const part of document.cookie.split(";")) {
    const [key, ...rest] = part.trim().split("=");
    if (key === name) return decodeURIComponent(rest.join("="));
  }
  return null;
}

/** Browser only: write a long-lived, site-wide, non-httpOnly cookie. */
export function writeBrowserCookie(name: string, value: string, maxAgeSeconds = ONE_YEAR_SECONDS) {
  if (typeof document === "undefined") return;
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; Max-Age=${maxAgeSeconds}; SameSite=Lax${secure}`;
}
