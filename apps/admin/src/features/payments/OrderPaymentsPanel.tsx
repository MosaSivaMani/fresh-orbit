import { type Id } from "../../api/types/common";

/**
 * Payments, refunds and refund requests for one order, shown on the order detail "Payment" tab
 * (`GET /admin/orders/{orderId}/payments`, `POST /admin/orders/{orderId}/refunds`). Owned by the payments
 * feature; the order detail page only places it.
 */
export function OrderPaymentsPanel({ orderId }: { orderId: Id }) {
  void orderId;
  return null;
}
