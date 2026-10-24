// Customer sign-in shapes (docs/api §4.1), generated from the backend OpenAPI document (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";

export type OtpRequestBody = Schema<"OtpRequestRequest">;

export type OtpChallenge = Schema<"OtpRequestResponse">;

/** Backend `POST /auth/otp/verify` body. The website's BFF adds `guestCartToken` from its httpOnly cookie. */
export type OtpVerifyBody = Schema<"OtpVerifyRequest">;

export type SessionUser = Schema<"CustomerLoginUser">;

/** Backend `POST /auth/otp/verify` response. */
export type OtpVerifyResponse = Schema<"CustomerLoginResponse">;

/** Backend `POST /auth/refresh` response. */
export type RefreshResponse = Schema<"SessionTokensResponse">;

/** What the website's own `/bff/auth/*` handlers return to the browser: never the refresh token. */
export interface BrowserSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: SessionUser | null;
}
