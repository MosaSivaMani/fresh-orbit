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

/** The variant fields inventory responses carry: Catalog's names and SKU, null for a variant Catalog no longer has. */
export interface VariantNames {
  variantId: string;
  productName?: string | null;
  variantName?: string | null;
  sku?: string | null;
}

/**
 * "Seer fish · Steaks" with the SKU, as the server names the variant; a short id (full id as a tooltip) when the
 * server has no name for it.
 */
export function VariantRef({ item }: { item: VariantNames }) {
  if (!item.productName) {
    return (
      <span className="font-mono text-xs" title={item.variantId}>
        …{item.variantId.slice(-12)}
      </span>
    );
  }
  return (
    <span title={item.variantId}>
      {item.variantName ? `${item.productName} · ${item.variantName}` : item.productName}
      {item.sku && <span className="text-text-muted ml-1.5 font-mono text-xs">{item.sku}</span>}
    </span>
  );
}
