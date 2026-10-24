import { IF_NONE_MATCH_ANY, type Page, type RequestOptions } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type BulkPriceRequest,
  type BulkPriceResponse,
  type ChargeSchedule,
  type PriceListItem,
  type PriceListQuery,
  type PriceSchedule,
  type ScheduleTaxRuleRequest,
  type SetChargeRequest,
  type SetPriceRequest,
  type TaxCategoryRules,
} from "./types/pricing";

// docs/api/README.md §4.12 — prices per location, preparation charges, tax rules, bulk upload.

export const pricingKeys = {
  all: ["pricing"] as const,
  prices: (q: object) => ["pricing", "prices", q] as const,
  price: (locationId: Id, variantId: Id) => ["pricing", "price", locationId, variantId] as const,
  charge: (locationId: Id, variantId: Id, code: string) =>
    ["pricing", "charge", locationId, variantId, code] as const,
  taxRules: ["pricing", "tax-rules"] as const,
};

/**
 * Schedule writes quote the schedule version with `If-Match`, or `If-None-Match: *` when the schedule has no
 * period yet (version null) — docs/api §1.7 create-if-absent.
 */
export function schedulePrecondition(
  version: Version | null | undefined,
): Pick<RequestOptions, "ifMatch" | "headers"> {
  return version ? { ifMatch: version } : { headers: IF_NONE_MATCH_ANY };
}

export function usePrices(query: PriceListQuery | null) {
  const http = useHttp();
  return useQuery({
    queryKey: pricingKeys.prices(query ?? {}),
    queryFn: ({ signal }) =>
      http.get<Page<PriceListItem>>("/admin/pricing/prices", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
    enabled: query !== null,
  });
}

const pricePath = (locationId: Id, variantId: Id) =>
  `/admin/pricing/locations/${locationId}/variants/${variantId}/price`;

export function usePriceSchedule(locationId: Id | null, variantId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: pricingKeys.price(locationId ?? "", variantId),
    queryFn: ({ signal }) =>
      http.get<PriceSchedule>(pricePath(locationId ?? "", variantId), { signal }),
    enabled: locationId !== null,
  });
}

function useScheduleWrite<TVars, TResult>(
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<TResult>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: () => qc.invalidateQueries({ queryKey: pricingKeys.all }),
  });
}

export function useSetPrice() {
  return useScheduleWrite(
    (
      http,
      {
        locationId,
        variantId,
        body,
        version,
      }: { locationId: Id; variantId: Id; body: SetPriceRequest; version: Version | null },
    ) =>
      http.put<PriceSchedule>(
        pricePath(locationId, variantId),
        body,
        schedulePrecondition(version),
      ),
  );
}

/** End the current period now (stop selling at this location). */
export function useEndPrice() {
  return useScheduleWrite(
    (
      http,
      { locationId, variantId, version }: { locationId: Id; variantId: Id; version: Version },
    ) => http.delete<PriceSchedule>(pricePath(locationId, variantId), { ifMatch: version }),
  );
}

const chargePath = (locationId: Id, variantId: Id, code: string) =>
  `/admin/pricing/locations/${locationId}/variants/${variantId}/preparations/${encodeURIComponent(code)}/charge`;

export function useChargeSchedule(locationId: Id | null, variantId: Id, code: string) {
  const http = useHttp();
  return useQuery({
    queryKey: pricingKeys.charge(locationId ?? "", variantId, code),
    queryFn: ({ signal }) =>
      http.get<ChargeSchedule>(chargePath(locationId ?? "", variantId, code), { signal }),
    enabled: locationId !== null,
  });
}

export function useSetCharge() {
  return useScheduleWrite(
    (
      http,
      {
        locationId,
        variantId,
        code,
        body,
        version,
      }: {
        locationId: Id;
        variantId: Id;
        code: string;
        body: SetChargeRequest;
        version: Version | null;
      },
    ) =>
      http.put<ChargeSchedule>(
        chargePath(locationId, variantId, code),
        body,
        schedulePrecondition(version),
      ),
  );
}

export function useTaxRules() {
  const http = useHttp();
  return useQuery({
    queryKey: pricingKeys.taxRules,
    queryFn: ({ signal }) =>
      http.get<TaxCategoryRules[] | { items: TaxCategoryRules[] }>("/admin/pricing/tax-rules", {
        signal,
      }),
    select: (data) => (Array.isArray(data) ? data : data.items),
  });
}

/**
 * Tax rule schedules are versioned per category (G9): quote the category's `version` from the list, or
 * `If-None-Match: *` while the category has no period.
 */
export function useScheduleTaxRule() {
  return useScheduleWrite(
    (http, { body, version }: { body: ScheduleTaxRuleRequest; version: Version | null }) =>
      http.post<TaxCategoryRules>("/admin/pricing/tax-rules", body, schedulePrecondition(version)),
  );
}

export type BulkUpload =
  | { kind: "csv"; text: string; validFrom?: string | null; replaceFuture?: boolean }
  | { kind: "json"; body: BulkPriceRequest };

/**
 * All-or-nothing bulk set. `dryRun` validates and returns the diff without writing. Each call needs an
 * `Idempotency-Key`; a dry run and the apply that follows are different actions, so they use different keys.
 */
export function useBulkPrices() {
  return useScheduleWrite(
    (
      http,
      {
        upload,
        dryRun,
        idempotencyKey,
      }: { upload: BulkUpload; dryRun: boolean; idempotencyKey: string },
    ) => {
      if (upload.kind === "csv") {
        return http.post<BulkPriceResponse>("/admin/pricing/prices/bulk", upload.text, {
          query: {
            dryRun,
            validFrom: upload.validFrom ?? undefined,
            replaceFuture: upload.replaceFuture || undefined,
          },
          headers: { "Content-Type": "text/csv" },
          idempotencyKey,
        });
      }
      return http.post<BulkPriceResponse>("/admin/pricing/prices/bulk", upload.body, {
        query: { dryRun },
        idempotencyKey,
      });
    },
  );
}
