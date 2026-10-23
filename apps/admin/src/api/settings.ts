import { IF_NONE_MATCH_ANY, isApiError } from "@fresh/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type BusinessProfile,
  type NumberSeries,
  type NumberSeriesList,
  type OrderPolicy,
  type OrderPolicyList,
  type SaveBusinessProfileRequest,
  type SaveNumberSeriesRequest,
  type SaveOrderPolicyRequest,
} from "./types/settings";

// docs/api/README.md §4.21 — business profile, order policies, number series (settings.manage).
// Writes quote `If-Match` to update, or `If-None-Match: *` to create a record that does not exist yet.

export const settingsKeys = {
  all: ["settings"] as const,
  businessProfile: ["settings", "business-profile"] as const,
  orderPolicies: ["settings", "order-policies"] as const,
  numberSeries: ["settings", "number-series"] as const,
};

/** `If-Match: "v"` when the record exists, `If-None-Match: *` to create it. */
function writeCondition(version: Version | null) {
  return version ? { ifMatch: version } : { headers: IF_NONE_MATCH_ANY };
}

/** The profile, or `null` when none has been saved yet (404 / CONFIGURATION_MISSING → create mode). */
export function useBusinessProfile() {
  const http = useHttp();
  return useQuery({
    queryKey: settingsKeys.businessProfile,
    queryFn: async ({ signal }) => {
      try {
        const res = await http.request<BusinessProfile>("GET", "/admin/settings/business-profile", {
          signal,
        });
        return { ...res.data, version: res.data.version ?? res.version ?? "" };
      } catch (error) {
        if (isApiError(error) && (error.status === 404 || error.code === "CONFIGURATION_MISSING")) {
          return null;
        }
        throw error;
      }
    },
  });
}

export function useSaveBusinessProfile() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      body,
      version,
    }: {
      body: SaveBusinessProfileRequest;
      version: Version | null;
    }) =>
      http.put<BusinessProfile>("/admin/settings/business-profile", body, writeCondition(version)),
    onSuccess: (data) => qc.setQueryData(settingsKeys.businessProfile, data),
  });
}

export function useOrderPolicies() {
  const http = useHttp();
  return useQuery({
    queryKey: settingsKeys.orderPolicies,
    queryFn: ({ signal }) =>
      http.get<OrderPolicyList>("/admin/settings/order-policies", { signal }),
    select: (data) => data.items,
  });
}

/** `key` is a city id or `default`. */
export function useSaveOrderPolicy() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      key,
      body,
      version,
    }: {
      key: Id | "default";
      body: SaveOrderPolicyRequest;
      version: Version | null;
    }) =>
      http.put<OrderPolicy>(`/admin/settings/order-policies/${key}`, body, writeCondition(version)),
    onSuccess: () => qc.invalidateQueries({ queryKey: settingsKeys.orderPolicies }),
  });
}

export function useNumberSeries() {
  const http = useHttp();
  return useQuery({
    queryKey: settingsKeys.numberSeries,
    queryFn: ({ signal }) =>
      http.get<NumberSeriesList>("/admin/settings/number-series", { signal }),
    select: (data) => data.items,
  });
}

export function useSaveNumberSeries() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      code,
      period,
      body,
      version,
    }: {
      code: string;
      period: string;
      body: SaveNumberSeriesRequest;
      version: Version | null;
    }) =>
      http.put<NumberSeries>(
        `/admin/settings/number-series/${encodeURIComponent(code)}/${encodeURIComponent(period)}`,
        body,
        writeCondition(version),
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: settingsKeys.numberSeries }),
  });
}
