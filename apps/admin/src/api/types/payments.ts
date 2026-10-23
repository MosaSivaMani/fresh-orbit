// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.16 (+ §2 payment codes); shapes match backend
// Payments.Application/Common/Contracts.cs and Payments.Infrastructure/Endpoints/PaymentsEndpoints.cs.

import type { CalendarDate, Id, Instant, Version } from "./common";

export type PaymentMethod = "COD" | "UPI_ON_DELIVERY" | "ONLINE";
export type PaymentStatus =
  "PENDING" | "AUTHORIZED" | "CAPTURED" | "COLLECTED" | "FAILED" | "CANCELLED";
export type CollectionMode = "CASH" | "UPI";
export type RefundStatus = "REQUESTED" | "APPROVED" | "PROCESSING" | "SUCCEEDED" | "FAILED";
export type RefundReason =
  "WEIGHT_SHORTFALL" | "CANCELLATION" | "COMPLAINT" | "SHORT_SUPPLY" | "GOODWILL" | "RETURN";
export type RefundMethod = "ORIGINAL" | "UPI" | "BANK_TRANSFER" | "CASH";
export type CashSettlementStatus = "OPEN" | "SETTLED" | "DISPUTED";
export type WebhookEventStatus = "RECEIVED" | "PROCESSED" | "IGNORED" | "ERROR" | "REJECTED";
export type IssueKind =
  | "AMOUNT_MISMATCH"
  | "MISSED_WEBHOOK"
  | "COLLECTION_DIFFERENCE"
  | "REFUND_CAPPED"
  | "REFUND_STUCK"
  | "DELIVERED_NOT_COLLECTED"
  | "UNSETTLED_CASH"
  | "SETTLEMENT_AGED"
  | "CASH_DIFFERENCE";
export type IssueStatus = "OPEN" | "AUTO_RESOLVED" | "RESOLVED";
export type IssueSubjectType = "PAYMENT" | "REFUND" | "SETTLEMENT" | "WEBHOOK_EVENT" | "ORDER";

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
  "DELIVERED_NOT_COLLECTED",
  "UNSETTLED_CASH",
  "SETTLEMENT_AGED",
  "CASH_DIFFERENCE",
];

/** Server-computed amounts of one payment. */
export interface PaymentSummary {
  paidPaise: number;
  refundedPaise: number;
  pendingRefundPaise: number;
  refundablePaise: number;
}

export interface Payment {
  id: Id;
  orderId: Id;
  customerId: Id;
  fulfillmentLocationId: Id;
  method: PaymentMethod;
  provider: string;
  status: PaymentStatus;
  amountPaise: number;
  currency: string;
  providerOrderId: string | null;
  providerPaymentId: string | null;
  expiresAt: Instant | null;
  capturedAt: Instant | null;
  collectedPaise: number | null;
  collectedAt: Instant | null;
  collectedByRiderId: Id | null;
  collectionMode: CollectionMode | null;
  collectionReference: string | null;
  cashSettlementId: Id | null;
  orderDeliveredAt: Instant | null;
  isExcess: boolean;
  failureCode: string | null;
  failureMessage: string | null;
  summary: PaymentSummary;
  version: Version;
}

/** `version` is the row version of the refund's payment (the aggregate root). */
export interface Refund {
  id: Id;
  orderId: Id;
  paymentId: Id;
  fulfillmentLocationId: Id;
  amountPaise: number;
  reasonCode: RefundReason;
  method: RefundMethod;
  status: RefundStatus;
  requestedBy: Id | null;
  requestedAt: Instant;
  approvedBy: Id | null;
  approvedAt: Instant | null;
  complaintId: Id | null;
  payeeUpiId: string | null;
  notes: string | null;
  failureReason: string | null;
  providerRefundId: string | null;
  manualReference: string | null;
  succeededAt: Instant | null;
  attemptCount: number;
  version: Version;
  paymentSummary: PaymentSummary;
}

export interface OrderPayments {
  orderId: Id;
  payments: Payment[];
  refunds: Refund[];
}

export interface WebhookEvent {
  id: Id;
  provider: string;
  providerEventId: string;
  eventType: string;
  signatureValid: boolean;
  status: WebhookEventStatus;
  receivedAt: Instant;
  processedAt: Instant | null;
  error: string | null;
}

export interface CashSettlement {
  id: Id;
  riderId: Id;
  fulfillmentLocationId: Id;
  date: CalendarDate;
  expectedPaise: number;
  receivedPaise: number;
  differencePaise: number;
  status: CashSettlementStatus;
  collectionsCount: number;
  recordedBy: Id | null;
  settledBy: Id | null;
  settledAt: Instant | null;
  note: string | null;
  disputeReason: string | null;
  version: Version;
}

export interface PaymentIssue {
  id: Id;
  kind: IssueKind;
  subjectType: IssueSubjectType;
  subjectId: Id;
  orderId: Id | null;
  fulfillmentLocationId: Id | null;
  details: string;
  status: IssueStatus;
  detectedAt: Instant;
  resolvedAt: Instant | null;
  resolvedBy: Id | null;
  note: string | null;
  version: Version;
}

/** Rider row from `GET /admin/delivery/riders` (Delivery; used only for the settlement picker). */
export interface RiderOption {
  id: Id;
  fulfillmentLocationId: Id;
  fullName: string;
  phone: string;
  isActive: boolean;
  version: Version;
}

export interface CreateRefundRequest {
  amountPaise: number;
  reasonCode: RefundReason;
  method: RefundMethod;
  complaintId: Id | null;
  upiId: string | null;
  notes: string | null;
}

export interface RejectRefundRequest {
  reason: string;
}

export interface MarkRefundPaidRequest {
  /** 4–64 characters (REFERENCE_INVALID). */
  reference: string;
}

export interface CreateCashSettlementRequest {
  riderId: Id;
  /** Local business date; future → DATE_IN_FUTURE. */
  date: CalendarDate;
  receivedPaise: number;
  note: string | null;
}

export interface DisputeCashSettlementRequest {
  reason: string;
}

export interface SettleCashSettlementRequest {
  note: string | null;
}

export interface ResolveIssueRequest {
  note: string;
}
