import { type ApiResponse, type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type Coupon,
  type CouponListQuery,
  type CouponRedemption,
  type CouponRequest,
} from "./types/promotions";

// docs/api/README.md §4.13 — admin coupons (promotions.manage).

export const promotionKeys = {
  all: ["promotions"] as const,
  coupons: (q: object) => ["promotions", "coupons", q] as const,
  coupon: (id: Id) => ["promotions", "coupon", id] as const,
  redemptions: (id: Id, q: object) => ["promotions", "redemptions", id, q] as const,
};

function withVersion(res: ApiResponse<Coupon>): Coupon {
  return { ...res.data, version: res.data.version ?? res.version ?? "" };
}

export function useCoupons(query: CouponListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: promotionKeys.coupons(query),
    queryFn: ({ signal }) =>
      http.get<Page<Coupon>>("/admin/coupons", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useCoupon(couponId: Id, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: promotionKeys.coupon(couponId),
    queryFn: async ({ signal }) =>
      withVersion(await http.request<Coupon>("GET", `/admin/coupons/${couponId}`, { signal })),
    enabled: enabled && Boolean(couponId),
  });
}

export function useCouponRedemptions(couponId: Id, query: { page?: number; pageSize?: number }) {
  const http = useHttp();
  return useQuery({
    queryKey: promotionKeys.redemptions(couponId, query),
    queryFn: ({ signal }) =>
      http.get<Page<CouponRedemption>>(`/admin/coupons/${couponId}/redemptions`, {
        query: { ...query },
        signal,
      }),
    placeholderData: keepPreviousData,
  });
}

export function useCreateCoupon() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: CouponRequest) =>
      withVersion(await http.request<Coupon>("POST", "/admin/coupons", { body })),
    onSuccess: () => qc.invalidateQueries({ queryKey: promotionKeys.all }),
  });
}

export function useUpdateCoupon(couponId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ body, version }: { body: CouponRequest; version: Version }) =>
      withVersion(
        await http.request<Coupon>("PUT", `/admin/coupons/${couponId}`, { body, ifMatch: version }),
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: promotionKeys.all }),
  });
}

/** Pause or resume (docs/product/promotions.md §2). */
export function useCouponTransition(couponId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ action, version }: { action: "pause" | "resume"; version: Version }) =>
      http.post<Coupon>(`/admin/coupons/${couponId}/${action}`, undefined, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: promotionKeys.all }),
  });
}
