import { type ApiResponse, type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type AdminOrderDetail,
  type AdminOrderListQuery,
  type AdminOrderSummary,
  type CancelOrderRequest,
  type ComplaintDetail,
  type ComplaintSummary,
  type DecideComplaintRequest,
  type GoodwillRequest,
  type PackRequest,
  type PackResponse,
  type PickList,
  type ProposeSubstitutionRequest,
  type RecordSubstitutionResponseRequest,
} from "./types/orders";

// docs/api/README.md §4.15 — admin orders, pick list, weigh and pack, substitutions, cancel, complaints.

export const orderKeys = {
  all: ["orders"] as const,
  list: (q: object) => ["orders", "list", q] as const,
  detail: (id: Id) => ["orders", "detail", id] as const,
  pickList: (q: object) => ["orders", "pick-list", q] as const,
  complaints: (q: object) => ["orders", "complaints", q] as const,
  complaint: (id: Id) => ["orders", "complaint", id] as const,
};

/** The detail plus the `ETag` version (the body's `version` is preferred; the header is the fallback). */
export type Versioned<T> = T & { version: Version };

function withVersion<T extends { version?: Version }>(res: ApiResponse<T>): Versioned<T> {
  return { ...res.data, version: res.data.version ?? res.version ?? "" } as Versioned<T>;
}

export function useOrders(query: AdminOrderListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: orderKeys.list(query),
    queryFn: ({ signal }) =>
      http.get<Page<AdminOrderSummary>>("/admin/orders", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useOrder(orderId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: orderKeys.detail(orderId),
    queryFn: async ({ signal }) =>
      withVersion(
        await http.request<AdminOrderDetail>("GET", `/admin/orders/${orderId}`, { signal }),
      ),
  });
}

export function usePickList(query: { fulfillmentLocationId: Id; slotId: Id } | null) {
  const http = useHttp();
  return useQuery({
    queryKey: orderKeys.pickList(query ?? {}),
    queryFn: ({ signal }) =>
      http.get<PickList>("/admin/orders/pick-list", { query: { ...query }, signal }),
    enabled: query !== null,
  });
}

/** Invalidate everything about one order after a write (list rows, detail, pick list, dashboard). */
function useOrderWrite<TVars, TResult>(
  orderId: Id,
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<TResult>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: async () => {
      await Promise.all([
        qc.invalidateQueries({ queryKey: orderKeys.detail(orderId) }),
        qc.invalidateQueries({ queryKey: ["orders", "list"] }),
        qc.invalidateQueries({ queryKey: ["orders", "pick-list"] }),
        qc.invalidateQueries({ queryKey: ["reports", "dashboard"] }),
      ]);
    },
  });
}

export function useStartPreparing(orderId: Id) {
  return useOrderWrite(orderId, (http, version: Version) =>
    http.post<AdminOrderDetail>(`/admin/orders/${orderId}/start-preparing`, undefined, {
      ifMatch: version,
    }),
  );
}

/** Thrown by {@link useStartPreparingMany}: which order failed, with the ApiError as `cause`. */
export class BulkOrderError extends Error {
  constructor(
    readonly orderId: Id,
    cause: unknown,
  ) {
    super("Start preparing failed.", { cause });
  }
}

/**
 * Start preparing several orders (orders list bulk action). One request per order, each quoting its own
 * version; stops at the first refusal. Resolves with the number started.
 */
export function useStartPreparingMany() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (orders: { orderId: Id; version: Version }[]) => {
      let started = 0;
      for (const { orderId, version } of orders) {
        try {
          await http.post(`/admin/orders/${orderId}/start-preparing`, undefined, {
            ifMatch: version,
          });
        } catch (error) {
          throw new BulkOrderError(orderId, error);
        }
        started += 1;
      }
      return started;
    },
    onSettled: () => qc.invalidateQueries({ queryKey: orderKeys.all }),
  });
}

/** Weigh and pack the whole order: every ACTIVE line once; 0 = short supplied (docs/api §4.15). */
export function usePackOrder(orderId: Id) {
  return useOrderWrite(
    orderId,
    (http, { body, version }: { body: PackRequest; version: Version }) =>
      http.post<PackResponse>(`/admin/orders/${orderId}/pack`, body, { ifMatch: version }),
  );
}

export function useCancelOrder(orderId: Id) {
  return useOrderWrite(
    orderId,
    (http, { body, version }: { body: CancelOrderRequest; version: Version }) =>
      http.post<AdminOrderDetail>(`/admin/orders/${orderId}/cancel`, body, { ifMatch: version }),
  );
}

export function useProposeSubstitution(orderId: Id) {
  return useOrderWrite(
    orderId,
    (
      http,
      { itemId, body, version }: { itemId: Id; body: ProposeSubstitutionRequest; version: Version },
    ) =>
      http.post<AdminOrderDetail>(`/admin/orders/${orderId}/items/${itemId}/substitutions`, body, {
        ifMatch: version,
      }),
  );
}

export function useRecordSubstitutionResponse(orderId: Id) {
  return useOrderWrite(
    orderId,
    (
      http,
      {
        substitutionId,
        body,
        version,
      }: { substitutionId: Id; body: RecordSubstitutionResponseRequest; version: Version },
    ) =>
      http.post<AdminOrderDetail>(
        `/admin/orders/${orderId}/substitutions/${substitutionId}/record-response`,
        body,
        { ifMatch: version },
      ),
  );
}

export function useGoodwill(orderId: Id) {
  return useOrderWrite(
    orderId,
    (http, { body, version }: { body: GoodwillRequest; version: Version }) =>
      http.post<AdminOrderDetail>(`/admin/orders/${orderId}/goodwill`, body, { ifMatch: version }),
  );
}

export function useComplaints(query: object) {
  const http = useHttp();
  return useQuery({
    queryKey: orderKeys.complaints(query),
    queryFn: ({ signal }) =>
      http.get<Page<ComplaintSummary>>("/admin/complaints", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useComplaint(complaintId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: orderKeys.complaint(complaintId),
    queryFn: async ({ signal }) =>
      withVersion(
        await http.request<ComplaintDetail>("GET", `/admin/complaints/${complaintId}`, { signal }),
      ),
  });
}

function useComplaintWrite<TVars>(
  complaintId: Id,
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<unknown>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: () => qc.invalidateQueries({ queryKey: orderKeys.all }),
  });
}

export function useDecideComplaint(complaintId: Id) {
  return useComplaintWrite(
    complaintId,
    (http, { body, version }: { body: DecideComplaintRequest; version: Version }) =>
      http.post<ComplaintDetail>(`/admin/complaints/${complaintId}/decide`, body, {
        ifMatch: version,
      }),
  );
}

export function useResolveComplaint(complaintId: Id) {
  return useComplaintWrite(complaintId, (http, version: Version) =>
    http.post<ComplaintDetail>(`/admin/complaints/${complaintId}/resolve`, undefined, {
      ifMatch: version,
    }),
  );
}
