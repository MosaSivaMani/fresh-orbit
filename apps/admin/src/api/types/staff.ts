// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.21 (staff, roles, permissions); shapes match backend
// Identity.Application/Staff/StaffContracts.cs and Identity.Application/Roles/RoleService.cs.

import type { Id, Instant, Version } from "./common";
import type { StaffRoleRef } from "./auth";

export type { StaffRoleRef };

export type UserStatus = "ACTIVE" | "LOCKED" | "DISABLED";

export interface Staff {
  id: Id;
  userId: Id;
  email: string | null;
  fullName: string;
  roles: StaffRoleRef[];
  allLocations: boolean;
  locationIds: Id[];
  isActive: boolean;
  userStatus: UserStatus;
  lastLoginAt: Instant | null;
  version: Version;
}

export interface StaffListQuery {
  q?: string;
  isActive?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateStaffRequest {
  email: string;
  fullName: string;
  roleIds: Id[];
  allLocations: boolean;
  locationIds: Id[] | null;
  temporaryPassword: string;
}

export interface UpdateStaffRequest {
  fullName: string;
  roleIds: Id[];
  allLocations: boolean;
  locationIds: Id[] | null;
  isActive: boolean;
}

export interface ResetStaffPasswordRequest {
  temporaryPassword: string;
}

export interface Role {
  id: Id;
  code: string;
  name: string;
  isSystem: boolean;
  permissions: string[];
  version: Version;
}

export interface RoleList {
  items: Role[];
}

export interface CreateRoleRequest {
  code: string;
  name: string;
  permissions: string[];
}

export interface UpdateRoleRequest {
  name: string;
  permissions: string[];
}

export interface PermissionInfo {
  code: string;
  description: string;
}

export interface PermissionList {
  items: PermissionInfo[];
}
