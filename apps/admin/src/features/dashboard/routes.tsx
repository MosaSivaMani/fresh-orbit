import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const dashboardRoutes: RouteObject[] = [
  screen(undefined, undefined, async () => (await import("./DashboardPage")).DashboardPage),
];
