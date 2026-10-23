import { type ApiResponse, type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type CashSettlement,
  type CreateCashSettlementRequest,
  type CreateRefundRequest,
  type MarkRefundPaidRequest,
  type OrderPayments,
  type Payment,
  type PaymentIssue,
  type Refund,
  type RejectRefundRequest,
  type RiderOption,
  type WebhookEvent,
} from "./types/payments";

// docs/api/README.md §4.16 — payments, webhook events, refunds, cash settlements, payment issues.

export const paymentKeys = {
  all: ["payments"] as const,
  payments: (q: object) => ["payments", "list", q] as const,
  payment: (id: Id) => ["payments", "payment", id] as const,
  orderPayments: (orderId: Id) => ["payments", "order", orderId] as const,
  webhookEvents: (q: object) => ["payments", "webhook-events", q] as const,
  refunds: (q: object) => ["payments", "refunds", q] as const,
  refund: (id: Id) => ["payments", "refund", id] as const,
  settlements: (q: object) => ["payments", "cash-settlements", q] as const,
  settlement: (id: Id) => ["payments", "cash-settlement", id] as const,
  issues: (q: object) => ["payments", "issues", q] as const,
  riders: (q: object) => ["payments", "riders", q] as const,
};

type Query = Record<string, string | number | string[] | undefined>;

function withVersion<T extends { version: Version }>(res: ApiResponse<T>): T {
  return { ...res.data, version: res.data.version || res.version || "" };
}

function useList<T>(key: readonly unknown[], path: string, query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => http.get<Page<T>>(path, { query, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function usePayments(query: Query) {
  return useList<Payment>(paymentKeys.payments(query), "/admin/payments", query);
}

export function usePayment(paymentId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: paymentKeys.payment(paymentId),
    queryFn: async ({ signal }) =>
      withVersion(await http.request<Payment>("GET", `/admin/payments/${paymentId}`, { signal })),
  });
}

export function useOrderPayments(orderId: Id, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: paymentKeys.orderPayments(orderId),
    queryFn: ({ signal }) =>
      http.get<OrderPayments>(`/admin/orders/${orderId}/payments`, { signal }),
    enabled,
  });
}

export function useWebhookEvents(query: Query, enabled = true) {
  return useList<WebhookEvent>(
    paymentKeys.webhookEvents(query),
    "/admin/webhook-events",
    query,
    enabled,
  );
}

export function useReprocessWebhookEvent() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (eventId: Id) =>
      http.post<WebhookEvent>(`/admin/webhook-events/${eventId}/reprocess`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["payments", "webhook-events"] }),
  });
}

export function useRefunds(query: Query) {
  return useList<Refund>(paymentKeys.refunds(query), "/admin/refunds", query);
}

export function useRefund(refundId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: paymentKeys.refund(refundId),
    queryFn: async ({ signal }) =>
      withVersion(await http.request<Refund>("GET", `/admin/refunds/${refundId}`, { signal })),
  });
}

/** Refund create: `Idempotency-Key` required (docs/api §1.8); the caller keeps one key per user action. */
export function useCreateRefund(orderId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, idempotencyKey }: { body: CreateRefundRequest; idempotencyKey: string }) =>
      http.post<Refund>(`/admin/orders/${orderId}/refunds`, body, { idempotencyKey }),
    onSuccess: () => qc.invalidateQueries({ queryKey: paymentKeys.all }),
  });
}

export type RefundAction =
  | { action: "approve"; version: Version }
  | { action: "reject"; version: Version; body: RejectRefundRequest }
  | { action: "mark-paid"; version: Version; body: MarkRefundPaidRequest };

export function useRefundAction(refundId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: RefundAction) =>
      http.post<Refund>(
        `/admin/refunds/${refundId}/${vars.action}`,
        "body" in vars ? vars.body : undefined,
        { ifMatch: vars.version },
      ),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: paymentKeys.all });
      await qc.invalidateQueries({ queryKey: ["reports", "dashboard"] });
    },
  });
}

export function useCashSettlements(query: Query) {
  return useList<CashSettlement>(paymentKeys.settlements(query), "/admin/cash-settlements", query);
}

/** Settlement create: `Idempotency-Key` required; the server computes expected and difference. */
export function useCreateCashSettlement() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      body,
      idempotencyKey,
    }: {
      body: CreateCashSettlementRequest;
      idempotencyKey: string;
    }) => http.post<CashSettlement>("/admin/cash-settlements", body, { idempotencyKey }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["payments", "cash-settlements"] }),
  });
}

export type SettlementAction =
  | { action: "dispute"; version: Version; body: { reason: string } }
  | { action: "settle"; version: Version; body: { note: string | null } };

export function useSettlementAction(settlementId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: SettlementAction) =>
      http.post<CashSettlement>(
        `/admin/cash-settlements/${settlementId}/${vars.action}`,
        vars.body,
        {
          ifMatch: vars.version,
        },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["payments", "cash-settlements"] }),
  });
}

export function usePaymentIssues(query: Query) {
  return useList<PaymentIssue>(paymentKeys.issues(query), "/admin/payment-issues", query);
}

export function useResolvePaymentIssue(issueId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ note, version }: { note: string; version: Version }) =>
      http.post<PaymentIssue>(
        `/admin/payment-issues/${issueId}/resolve`,
        { note },
        { ifMatch: version },
      ),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ["payments", "issues"] });
      await qc.invalidateQueries({ queryKey: ["reports", "dashboard"] });
    },
  });
}

/**
 * Riders for the settlement picker. `GET /admin/delivery/riders` needs `delivery.manage`; staff with only
 * `cash.settle` get 403 and the form falls back to a rider-id field (API gap, docs/design/admin.md).
 */
export function useRiderOptions(fulfillmentLocationId: Id | null, enabled = true) {
  const http = useHttp();
  const query: Query = {
    pageSize: 100,
    ...(fulfillmentLocationId ? { fulfillmentLocationId } : {}),
  };
  return useQuery({
    queryKey: paymentKeys.riders(query),
    queryFn: ({ signal }) =>
      http.get<Page<RiderOption>>("/admin/delivery/riders", { query, signal }),
    enabled,
    retry: false,
    staleTime: 5 * 60_000,
  });
}
