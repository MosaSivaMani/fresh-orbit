// Checkout and orders (docs/api §4.8). Writes that need `Idempotency-Key` take it from the caller, who keeps one
// key per user action and reuses it on retries.
import { type HttpClient } from "@fresh/core";
import { type Cart } from "../types/cart";
import {
  type CheckoutPreview,
  type CheckoutPreviewRequest,
  type PlaceOrderRequest,
} from "../types/checkout";
import { type Page } from "../types/common";
import {
  type CancelReasonCode,
  type Complaint,
  type InvoiceResponse,
  type OrderDetail,
  type OrderListItem,
  type OrderTracking,
  type RaiseComplaintRequest,
} from "../types/orders";

export function previewCheckout(
  http: HttpClient,
  body: CheckoutPreviewRequest,
): Promise<CheckoutPreview> {
  return http.post<CheckoutPreview>("/checkout/preview", body);
}

export function placeOrder(
  http: HttpClient,
  body: PlaceOrderRequest,
  idempotencyKey: string,
): Promise<OrderDetail> {
  return http.post<OrderDetail>("/orders", body, { idempotencyKey });
}

export function listOrders(
  http: HttpClient,
  params: { page?: number; pageSize?: number } = {},
): Promise<Page<OrderListItem>> {
  // Newest first is the server's order; `GET /orders` takes no sort parameter.
  return http.get<Page<OrderListItem>>("/orders", {
    query: { page: params.page, pageSize: params.pageSize },
  });
}

export function getOrder(http: HttpClient, orderId: string): Promise<OrderDetail> {
  return http.get<OrderDetail>(`/orders/${encodeURIComponent(orderId)}`);
}

export function getTracking(http: HttpClient, orderId: string): Promise<OrderTracking> {
  return http.get<OrderTracking>(`/orders/${encodeURIComponent(orderId)}/tracking`);
}

export function cancelOrder(
  http: HttpClient,
  orderId: string,
  reasonCode: CancelReasonCode,
  idempotencyKey: string,
): Promise<OrderDetail> {
  return http.post<OrderDetail>(
    `/orders/${encodeURIComponent(orderId)}/cancel`,
    { reasonCode },
    { idempotencyKey },
  );
}

/** JSON body; photos follow one by one through `addComplaintPhoto` (gap W12 closed). */
export function raiseComplaint(
  http: HttpClient,
  orderId: string,
  body: RaiseComplaintRequest,
  idempotencyKey: string,
): Promise<Complaint> {
  return http.post<Complaint>(`/orders/${encodeURIComponent(orderId)}/complaints`, body, {
    idempotencyKey,
  });
}

/** Adds one photo to a complaint (multipart field `file`); returns the complaint with its photos. */
export function addComplaintPhoto(
  http: HttpClient,
  orderId: string,
  complaintId: string,
  file: File,
): Promise<Complaint> {
  const body = new FormData();
  body.append("file", file, file.name);
  return http.post<Complaint>(
    `/orders/${encodeURIComponent(orderId)}/complaints/${encodeURIComponent(complaintId)}/photos`,
    body,
  );
}

export function listComplaints(http: HttpClient, orderId: string): Promise<Complaint[]> {
  return http.get<Complaint[]>(`/orders/${encodeURIComponent(orderId)}/complaints`);
}

export function getInvoice(http: HttpClient, orderId: string): Promise<InvoiceResponse> {
  return http.get<InvoiceResponse>(`/orders/${encodeURIComponent(orderId)}/invoice`);
}

/** Accepts or rejects a substitution the store proposed; returns the re-priced order. */
export function answerSubstitution(
  http: HttpClient,
  orderId: string,
  substitutionId: string,
  answer: "accept" | "reject",
): Promise<OrderDetail> {
  return http.post<OrderDetail>(
    `/orders/${encodeURIComponent(orderId)}/substitutions/${encodeURIComponent(substitutionId)}/${answer}`,
  );
}

/** Adds the order's sellable lines to the cart; returns the priced cart. */
export function reorder(http: HttpClient, orderId: string): Promise<Cart> {
  return http.post<Cart>(`/orders/${encodeURIComponent(orderId)}/reorder`);
}
