// Permission codes from docs/api/README.md §3 (+ `payments.manage`, ruling D15; `tax.manage`, D35). The API enforces them; the
// admin uses them only to decide which menus, routes and buttons to render.

export const PERMISSIONS = [
  "catalog.manage",
  "pricing.manage",
  "tax.manage",
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

/**
 * A route or action is shown when the staff member holds the permission, every permission of a list, or at
 * least one of an `anyOf` list (the tax rules screen reads with `pricing.manage` or `tax.manage`, D35).
 */
export type PermissionRequirement =
  Permission | readonly Permission[] | { readonly anyOf: readonly Permission[] };

export function hasPermission(
  granted: ReadonlySet<string>,
  required: PermissionRequirement | undefined,
): boolean {
  if (required === undefined) return true;
  if (typeof required === "string") return granted.has(required);
  if ("anyOf" in required) return required.anyOf.some((p) => granted.has(p));
  return required.every((p) => granted.has(p));
}

/** D35: tax rules are read with `pricing.manage` or `tax.manage`; only `tax.manage` schedules a new period. */
export const TAX_RULES_READ: PermissionRequirement = { anyOf: ["pricing.manage", "tax.manage"] };
export const TAX_RULES_SCHEDULE: PermissionRequirement = "tax.manage";
