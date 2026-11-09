import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const operationsRoutes: RouteObject[] = [
  screen("settings/store-readiness", "settings.manage", () =>
    import("./StoreReadinessPage").then((m) => m.StoreReadinessPage),
  ),
  screen("settings/failed-messages", "platform.manage", () =>
    import("./DeadLettersPage").then((m) => m.DeadLettersPage),
  ),
];
