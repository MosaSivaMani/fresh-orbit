import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const paymentsRoutes: RouteObject[] = [
  {
    path: "payments",
    children: [
      screen(undefined, "payments.view", async () => (await import("./PaymentsPage")).PaymentsPage),
      screen(
        "webhook-events",
        "payments.view",
        async () => (await import("./WebhookEventsPage")).WebhookEventsPage,
      ),
      screen(
        ":paymentId",
        "payments.view",
        async () => (await import("./PaymentDetailPage")).PaymentDetailPage,
      ),
    ],
  },
  {
    path: "refunds",
    children: [
      screen(undefined, "payments.view", async () => (await import("./RefundsPage")).RefundsPage),
      screen(
        ":refundId",
        "payments.view",
        async () => (await import("./RefundDetailPage")).RefundDetailPage,
      ),
    ],
  },
  screen(
    "cash-settlements",
    "cash.settle",
    async () => (await import("./CashSettlementsPage")).CashSettlementsPage,
  ),
  screen(
    "payment-issues",
    "payments.view",
    async () => (await import("./PaymentIssuesPage")).PaymentIssuesPage,
  ),
];
