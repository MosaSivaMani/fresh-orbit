import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const deliveryRoutes: RouteObject[] = [
  screen(
    "delivery",
    "delivery.manage",
    async () => (await import("./ShipmentsPage")).ShipmentsPage,
  ),
  screen(
    "delivery/shipments/:shipmentId",
    "delivery.manage",
    async () => (await import("./ShipmentDetailPage")).ShipmentDetailPage,
  ),
  screen("delivery/slots", "delivery.manage", async () => (await import("./SlotsPage")).SlotsPage),
  screen(
    "delivery/templates",
    "delivery.manage",
    async () => (await import("./TemplatesPage")).TemplatesPage,
  ),
  screen(
    "delivery/riders",
    "delivery.manage",
    async () => (await import("./RidersPage")).RidersPage,
  ),
];
