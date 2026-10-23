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
  type ComplaintKind,
  type InvoiceResponse,
  type OrderDetail,
  type OrderListItem,
  type OrderTracking,
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
  return http.get<Page<OrderListItem>>("/orders", {
    query: { sort: "-placedAt", page: params.page, pageSize: params.pageSize },
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

export interface ComplaintInput {
  orderItemId: string | null;
  kind: ComplaintKind;
  description: string;
  images: File[];
}

/** Multipart (`orderItemId`, `kind`, `description`, up to 4 `images`). */
export function raiseComplaint(
  http: HttpClient,
  orderId: string,
  input: ComplaintInput,
  idempotencyKey: string,
): Promise<Complaint> {
  const form = new FormData();
  if (input.orderItemId) form.append("orderItemId", input.orderItemId);
  form.append("kind", input.kind);
  form.append("description", input.description);
  for (const image of input.images) form.append("images", image);
  return http.post<Complaint>(`/orders/${encodeURIComponent(orderId)}/complaints`, form, {
    idempotencyKey,
  });
}

export function listComplaints(http: HttpClient, orderId: string): Promise<Complaint[]> {
  return http
    .get<Complaint[] | { items: Complaint[] }>(`/orders/${encodeURIComponent(orderId)}/complaints`)
    .then((data) => (Array.isArray(data) ? data : data.items));
}

export function getInvoice(http: HttpClient, orderId: string): Promise<InvoiceResponse> {
  return http.get<InvoiceResponse>(`/orders/${encodeURIComponent(orderId)}/invoice`);
}

/** Adds the order's sellable lines to the cart; returns the priced cart. */
export function reorder(http: HttpClient, orderId: string): Promise<Cart> {
  return http.post<Cart>(`/orders/${encodeURIComponent(orderId)}/reorder`);
}
