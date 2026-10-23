import { type RouteObject } from "react-router";
import { notBuilt } from "../../app/route-helpers";

export const settingsRoutes: RouteObject[] = [
  notBuilt("settings/staff", "staff.manage", "Staff & roles"),
  notBuilt("settings/business-profile", "settings.manage", "Business profile"),
  notBuilt("settings/order-policies", "settings.manage", "Order policies"),
  notBuilt("settings/number-series", "settings.manage", "Number series"),
  notBuilt("settings/notification-templates", "notifications.manage", "Notification templates"),
  notBuilt("settings/audit", "audit.view", "Audit log"),
  notBuilt("account", undefined, "My account"),
];
