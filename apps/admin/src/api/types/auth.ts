// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.1; shapes match backend Identity.Application/Staff/StaffContracts.cs.

import type { Id, Instant } from "./common";

export interface StaffLoginRequest {
  email: string;
  password: string;
  deviceLabel?: string | null;
}

export interface StaffRoleRef {
  id: Id;
  code: string;
  name: string;
}

/** `GET /admin/auth/me` and `staff` in the login response. */
export interface StaffProfile {
  staffId: Id;
  userId: Id;
  email: string | null;
  fullName: string;
  roles: StaffRoleRef[];
  permissions: string[];
  allLocations: boolean;
  locationIds: Id[];
}

export interface StaffLoginResponse {
  accessToken: string;
  accessTokenExpiresAt: Instant;
  refreshToken: string;
  refreshTokenExpiresAt: Instant;
  staff: StaffProfile;
}

export interface LogoutRequest {
  refreshToken: string | null;
  allDevices?: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
