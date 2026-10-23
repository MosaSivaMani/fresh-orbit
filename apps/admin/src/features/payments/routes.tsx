import { type RouteObject } from "react-router";
import { notBuilt } from "../../app/route-helpers";

export const paymentsRoutes: RouteObject[] = [
  notBuilt("payments", "payments.view", "Payments"),
  notBuilt("refunds", "payments.view", "Refunds"),
  notBuilt("cash-settlements", "cash.settle", "Cash settlements"),
  notBuilt("payment-issues", "payments.view", "Payment issues"),
];
