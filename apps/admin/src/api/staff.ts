import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type CreateRoleRequest,
  type CreateStaffRequest,
  type PermissionList,
  type ResetStaffPasswordRequest,
  type Role,
  type RoleList,
  type Staff,
  type StaffListQuery,
  type UpdateRoleRequest,
  type UpdateStaffRequest,
} from "./types/staff";

// docs/api/README.md §4.21 — staff, roles and the permission catalogue (staff.manage).

export const staffKeys = {
  all: ["staff"] as const,
  list: (q: object) => ["staff", "list", q] as const,
  roles: ["staff", "roles"] as const,
  permissions: ["staff", "permissions"] as const,
};

export function useStaffList(query: StaffListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: staffKeys.list(query),
    queryFn: ({ signal }) => http.get<Page<Staff>>("/admin/staff", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useCreateStaff() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateStaffRequest) => http.post<Staff>("/admin/staff", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}

export function useUpdateStaff() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body, version }: { id: Id; body: UpdateStaffRequest; version: Version }) =>
      http.put<Staff>(`/admin/staff/${id}`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}

export function useResetStaffPassword() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      body,
      version,
    }: {
      id: Id;
      body: ResetStaffPasswordRequest;
      version: Version;
    }) => http.post<Staff>(`/admin/staff/${id}/reset-password`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}

export function useRoles(enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: staffKeys.roles,
    queryFn: ({ signal }) => http.get<RoleList>("/admin/roles", { signal }),
    select: (data) => data.items,
    enabled,
  });
}

export function usePermissionCatalogue(enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: staffKeys.permissions,
    queryFn: ({ signal }) => http.get<PermissionList>("/admin/permissions", { signal }),
    select: (data) => data.items,
    staleTime: 30 * 60_000,
    enabled,
  });
}

export function useCreateRole() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateRoleRequest) => http.post<Role>("/admin/roles", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.roles }),
  });
}

export function useUpdateRole() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body, version }: { id: Id; body: UpdateRoleRequest; version: Version }) =>
      http.put<Role>(`/admin/roles/${id}`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}

export function useDeleteRole() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version }: { id: Id; version: Version }) =>
      http.delete(`/admin/roles/${id}`, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.roles }),
  });
}
