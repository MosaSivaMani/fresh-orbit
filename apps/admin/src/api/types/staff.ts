// Staff, roles and permissions (docs/api §4.21), generated from the backend OpenAPI document.

import type { StaffRoleRef } from "./auth";
import type { Coded, Schema } from "./common";

export type { StaffRoleRef };

export type UserStatus = "ACTIVE" | "LOCKED" | "DISABLED";

export type Staff = Coded<Schema<"StaffResponse">, { userStatus: UserStatus }>;

export interface StaffListQuery {
  q?: string;
  isActive?: string;
  page?: number;
  pageSize?: number;
}

export type CreateStaffRequest = Schema<"CreateStaffRequest">;

export type UpdateStaffRequest = Schema<"UpdateStaffRequest">;

export type ResetStaffPasswordRequest = Schema<"ResetStaffPasswordRequest">;

export type Role = Schema<"RoleResponse">;

export type RoleList = Schema<"RoleListResponse">;

export type CreateRoleRequest = Schema<"CreateRoleRequest">;

export type UpdateRoleRequest = Schema<"UpdateRoleRequest">;

export type PermissionInfo = Schema<"PermissionResponse">;

export type PermissionList = Schema<"PermissionListResponse">;
