import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const reportsRoutes: RouteObject[] = [
  screen("reports", "reports.view", () => import("./ReportsPage").then((m) => m.ReportsPage)),
];
