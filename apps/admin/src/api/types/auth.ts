// Staff sign-in (docs/api §4.1), generated from the backend OpenAPI document.

import type { RequestBody, Schema } from "./common";

export type StaffLoginRequest = RequestBody<Schema<"StaffLoginRequest">>;

export type StaffRoleRef = Schema<"StaffRoleRef">;

/** `GET /admin/auth/me` and `staff` in the login response. */
export type StaffProfile = Schema<"StaffProfileResponse">;

export type StaffLoginResponse = Schema<"StaffLoginResponse">;

export type LogoutRequest = Schema<"LogoutRequest">;

export type ChangePasswordRequest = Schema<"ChangePasswordRequest">;
