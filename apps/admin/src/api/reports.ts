import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id } from "./types/common";
import {
  type DashboardResponse,
  type ReportKey,
  type ReportQuery,
  type ReportResponse,
} from "./types/reports";

// docs/api/README.md §4.20 — reports and the live dashboard. Every number is computed by the server.

export const reportKeys = {
  all: ["reports"] as const,
  dashboard: (q: object) => ["reports", "dashboard", q] as const,
  report: (key: ReportKey, q: object) => ["reports", key, q] as const,
};

/** Drops empty values so the URL carries only the filters in use. */
export function cleanQuery(query: object): Record<string, string | number> {
  return Object.fromEntries(
    Object.entries(query).filter(([, v]) => v !== undefined && v !== null && v !== ""),
  ) as Record<string, string | number>;
}

export function useDashboard(
  query: { fulfillmentLocationId?: Id; toDate?: string },
  enabled = true,
) {
  const http = useHttp();
  return useQuery({
    queryKey: reportKeys.dashboard(query),
    queryFn: ({ signal }) =>
      http.get<DashboardResponse>("/admin/reports/dashboard", { query: cleanQuery(query), signal }),
    refetchInterval: 60_000,
    enabled,
  });
}

export function useReport(key: ReportKey, query: ReportQuery, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: reportKeys.report(key, query),
    queryFn: ({ signal }) =>
      http.get<ReportResponse>(`/admin/reports/${key}`, { query: cleanQuery(query), signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/**
 * Downloads `format=csv` through the authenticated client (a plain link cannot carry the Bearer header) and
 * saves it under the server's file name. Errors arrive as the usual ApiError.
 */
export function useReportCsv() {
  const http = useHttp();
  return useMutation({
    mutationFn: async ({ key, query }: { key: ReportKey; query: ReportQuery }) => {
      const response = await http.request<string>("GET", `/admin/reports/${key}`, {
        query: cleanQuery({ ...query, page: undefined, pageSize: undefined, format: "csv" }),
        headers: { Accept: "text/csv" },
      });
      const disposition = response.headers.get("Content-Disposition") ?? "";
      const name = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `${key}.csv`;
      // Keep the UTF-8 byte-order mark (fetch's text() drops it) so spreadsheet apps read ₹ and names right.
      saveBlob(new Blob(["﻿", response.data ?? ""], { type: "text/csv;charset=utf-8" }), name);
      return name;
    },
  });
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}
