import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const settingsRoutes: RouteObject[] = [
  screen("settings/staff", "staff.manage", () => import("./StaffPage").then((m) => m.StaffPage)),
  screen("settings/business-profile", "settings.manage", () =>
    import("./BusinessProfilePage").then((m) => m.BusinessProfilePage),
  ),
  screen("settings/order-policies", "settings.manage", () =>
    import("./OrderPoliciesPage").then((m) => m.OrderPoliciesPage),
  ),
  screen("settings/number-series", "settings.manage", () =>
    import("./NumberSeriesPage").then((m) => m.NumberSeriesPage),
  ),
  screen("settings/notification-templates", "notifications.manage", () =>
    import("./NotificationTemplatesPage").then((m) => m.NotificationTemplatesPage),
  ),
  screen("settings/audit", "audit.view", () =>
    import("./AuditLogPage").then((m) => m.AuditLogPage),
  ),
  screen("account", undefined, () => import("./AccountPage").then((m) => m.AccountPage)),
];
