import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const inventoryRoutes: RouteObject[] = [
  {
    path: "inventory",
    children: [
      screen(
        undefined,
        "inventory.view",
        async () => (await import("./StockListPage")).StockListPage,
      ),
      screen(
        "stock/:stockItemId",
        "inventory.view",
        async () => (await import("./StockItemPage")).StockItemPage,
      ),
      screen(
        "receive",
        "inventory.receive",
        async () => (await import("./ReceiveStockPage")).ReceiveStockPage,
      ),
      screen("batches", "inventory.view", async () => (await import("./BatchesPage")).BatchesPage),
      screen(
        "movements",
        "inventory.view",
        async () => (await import("./MovementsPage")).MovementsPage,
      ),
      screen(
        "cold-chain",
        "inventory.view",
        async () => (await import("./ColdChainPage")).ColdChainPage,
      ),
      screen(
        "reservations",
        "inventory.view",
        async () => (await import("./ReservationsPage")).ReservationsPage,
      ),
    ],
  },
];
