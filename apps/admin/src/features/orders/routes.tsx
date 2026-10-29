import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

// "orders/pick-list" is declared before "orders/:orderId"; React Router ranks static segments first anyway.
export const ordersRoutes: RouteObject[] = [
  screen("orders", "orders.view", async () => (await import("./OrdersListPage")).OrdersListPage),
  screen(
    "orders/pick-list",
    "orders.pack",
    async () => (await import("./PickListPage")).PickListPage,
  ),
  screen(
    "orders/:orderId",
    "orders.view",
    async () => (await import("./OrderDetailPage")).OrderDetailPage,
  ),
  screen(
    "orders/:orderId/invoice",
    "orders.view",
    async () => (await import("./InvoicePage")).InvoicePage,
  ),
  screen(
    "complaints",
    "complaints.manage",
    async () => (await import("./ComplaintsListPage")).ComplaintsListPage,
  ),
  screen(
    "complaints/:complaintId",
    "complaints.manage",
    async () => (await import("./ComplaintDetailPage")).ComplaintDetailPage,
  ),
];
