// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §1.2, §4.1 and docs/product/identity.md.

export interface OtpRequestBody {
  /** E.164, e.g. `+919876543210`. */
  phone: string;
  purpose: "LOGIN";
}

export interface OtpChallenge {
  challengeId: string;
  expiresAt: string;
  resendAfterSeconds: number;
  codeLength: number;
}

/** Backend `POST /auth/otp/verify` body. The website's BFF adds `guestCartToken` from its httpOnly cookie. */
export interface OtpVerifyBody {
  challengeId: string;
  phone: string;
  code: string;
  deviceLabel?: string | null;
  guestCartToken?: string | null;
}

export interface SessionUser {
  id: string;
  phone: string;
  fullName: string | null;
  isNewCustomer: boolean;
}

/** Backend `POST /auth/otp/verify` response. */
export interface OtpVerifyResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: SessionUser;
  cart: { id: string; mergedLineCount: number } | null;
}

/** Backend `POST /auth/refresh` response. */
export interface RefreshResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

/** What the website's own `/bff/auth/*` handlers return to the browser: never the refresh token. */
export interface BrowserSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: SessionUser | null;
}
