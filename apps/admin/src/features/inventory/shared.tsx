import { NavLink } from "react-router";
import { useCan } from "../../app/services";
import { type Permission } from "../../auth/permissions";

const TABS: readonly { to: string; label: string; perm: Permission; end?: boolean }[] = [
  { to: "/inventory", label: "Stock", perm: "inventory.view", end: true },
  { to: "/inventory/batches", label: "Batches", perm: "inventory.view" },
  { to: "/inventory/movements", label: "Movements", perm: "inventory.view" },
  { to: "/inventory/reservations", label: "Reservations", perm: "inventory.view" },
  { to: "/inventory/cold-chain", label: "Cold chain", perm: "inventory.view" },
  { to: "/inventory/receive", label: "Receive stock", perm: "inventory.receive" },
];

/** Section links between the inventory screens. */
export function InventoryNav() {
  const can = useCan();
  return (
    <nav aria-label="Inventory sections" className="border-border mb-6 border-b">
      <ul className="-mb-px flex flex-wrap gap-1">
        {TABS.filter((t) => can(t.perm)).map((t) => (
          <li key={t.to}>
            <NavLink
              to={t.to}
              end={t.end ?? false}
              className={({ isActive }) =>
                `inline-flex h-10 items-center border-b-2 px-3 text-sm font-medium ${
                  isActive
                    ? "border-accent text-text"
                    : "text-text-muted hover:text-text border-transparent"
                }`
              }
            >
              {t.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/**
 * Inventory responses carry `variantId` only (variant names live in Catalog, not in the inventory schema —
 * API gap noted in docs/design/admin.md). Shows a short id with the full id as a tooltip.
 */
export function VariantRef({ variantId }: { variantId: string }) {
  return (
    <span className="font-mono text-xs" title={variantId}>
      …{variantId.slice(-12)}
    </span>
  );
}
