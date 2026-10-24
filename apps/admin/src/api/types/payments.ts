// Payments, refunds, cash settlements, payment issues, webhook events (docs/api §4.16), generated from the backend
// OpenAPI document.

import type { Schema } from "./common";
import type { Rider } from "./delivery";

export type PaymentMethod = Schema<"PaymentsPaymentMethod">;
export type PaymentStatus = Schema<"PaymentStatus">;
export type CollectionMode = Schema<"CollectionMode">;
export type RefundStatus = Schema<"RefundStatus">;
export type RefundReason = Schema<"RefundReason">;
export type RefundMethod = Schema<"RefundMethod">;
export type CashSettlementStatus = Schema<"CashSettlementStatus">;
export type WebhookEventStatus = Schema<"WebhookEventStatus">;
export type IssueKind = Schema<"IssueKind">;
export type IssueStatus = Schema<"IssueStatus">;
export type IssueSubjectType = Schema<"IssueSubjectType">;

export const REFUND_REASONS: readonly RefundReason[] = [
  "COMPLAINT",
  "GOODWILL",
  "WEIGHT_SHORTFALL",
  "SHORT_SUPPLY",
  "CANCELLATION",
  "RETURN",
];
export const REFUND_METHODS: readonly RefundMethod[] = ["UPI", "BANK_TRANSFER", "CASH", "ORIGINAL"];
export const ISSUE_KINDS: readonly IssueKind[] = [
  "AMOUNT_MISMATCH",
  "MISSED_WEBHOOK",
  "COLLECTION_DIFFERENCE",
  "REFUND_CAPPED",
  "REFUND_STUCK",
  "REFUND_METHOD_REQUIRED",
  "DELIVERED_NOT_COLLECTED",
  "UNSETTLED_CASH",
  "SETTLEMENT_AGED",
  "CASH_DIFFERENCE",
  "WEBHOOK_ERROR",
];

/** Server-computed amounts of one payment. */
export type PaymentSummary = Schema<"PaymentSummaryResponse">;

export type Payment = Schema<"PaymentResponse">;

/** `version` is the row version of the refund's payment (the aggregate root). */
export type Refund = Schema<"RefundResponse">;

export type OrderPayments = Schema<"OrderPaymentsResponse">;

export type WebhookEvent = Schema<"WebhookEventResponse">;

export type CashSettlement = Schema<"CashSettlementResponse">;

export type PaymentIssue = Schema<"PaymentIssueResponse">;

/** Rider row from `GET /admin/delivery/riders` (Delivery; used only for the settlement picker). */
export type RiderOption = Rider;

export type CreateRefundRequest = Schema<"CreateRefundRequest">;

export type RejectRefundRequest = Schema<"RejectRefundRequest">;

/** `reference`: 4–64 characters (REFERENCE_INVALID). */
export type MarkRefundPaidRequest = Schema<"MarkRefundPaidRequest">;

/** `date`: local business date; future → DATE_IN_FUTURE. */
export type CreateCashSettlementRequest = Schema<"CreateCashSettlementRequest">;

export type DisputeCashSettlementRequest = Schema<"DisputeCashSettlementRequest">;

export type SettleCashSettlementRequest = Schema<"SettleCashSettlementRequest">;

export type ResolveIssueRequest = Schema<"ResolveIssueRequest">;
