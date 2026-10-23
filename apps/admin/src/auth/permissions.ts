// Permission codes from docs/api/README.md §3 (+ `payments.manage`, ruling D15). The API enforces them; the
// admin uses them only to decide which menus, routes and buttons to render.

export const PERMISSIONS = [
  "catalog.manage",
  "pricing.manage",
  "promotions.manage",
  "inventory.view",
  "inventory.receive",
  "inventory.adjust",
  "orders.view",
  "orders.pack",
  "orders.cancel",
  "complaints.manage",
  "refunds.request",
  "refunds.approve",
  "payments.view",
  "payments.manage",
  "cash.settle",
  "delivery.manage",
  "locations.manage",
  "cities.activate",
  "customers.view",
  "customers.manage",
  "notifications.manage",
  "reports.view",
  "settings.manage",
  "staff.manage",
  "audit.view",
] as const;

export type Permission = (typeof PERMISSIONS)[number];

/** A route or action is shown when the staff member holds every listed permission. */
export type PermissionRequirement = Permission | readonly Permission[];

export function hasPermission(
  granted: ReadonlySet<string>,
  required: PermissionRequirement | undefined,
): boolean {
  if (required === undefined) return true;
  const list: readonly Permission[] = typeof required === "string" ? [required] : required;
  return list.every((p) => granted.has(p));
}
