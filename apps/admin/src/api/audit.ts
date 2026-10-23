import { type Page } from "@fresh/core";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type AuditEntry, type AuditListQuery } from "./types/audit";

// docs/api/README.md §4.21 — audit log (audit.view). Every admin write is recorded by the API.

export const auditKeys = {
  list: (q: object) => ["audit", "list", q] as const,
};

export function useAuditLog(query: AuditListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: auditKeys.list(query),
    queryFn: ({ signal }) =>
      http.get<Page<AuditEntry>>("/admin/audit", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}
