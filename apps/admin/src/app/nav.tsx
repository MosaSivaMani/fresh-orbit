import {
  AlertTriangle,
  Banknote,
  BarChart3,
  Bell,
  Building2,
  ClipboardList,
  Hash,
  LayoutDashboard,
  MapPinned,
  MessageSquareWarning,
  Package,
  ReceiptIndianRupee,
  ScrollText,
  Settings2,
  ShoppingBag,
  Tag,
  TicketPercent,
  Truck,
  Undo2,
  Users,
  UserCog,
  Warehouse,
} from "lucide-react";
import { type ReactNode } from "react";
import { type PermissionRequirement } from "../auth/permissions";

export interface NavEntry {
  id: string;
  label: string;
  href: string;
  icon: ReactNode;
  /** Shown only to staff holding this permission (the API enforces it too). */
  perm?: PermissionRequirement;
  /** Other path prefixes that belong to this entry (for the highlighted item). */
  also?: readonly string[];
}

export interface NavGroup {
  id: string;
  label?: string;
  items: readonly NavEntry[];
}

export const NAVIGATION: readonly NavGroup[] = [
  {
    id: "operations",
    items: [
      {
        id: "dashboard",
        label: "Dashboard",
        href: "/",
        icon: <LayoutDashboard aria-hidden="true" />,
      },
      {
        id: "orders",
        label: "Orders",
        href: "/orders",
        icon: <ShoppingBag aria-hidden="true" />,
        perm: "orders.view",
      },
      {
        id: "complaints",
        label: "Complaints",
        href: "/complaints",
        icon: <MessageSquareWarning aria-hidden="true" />,
        perm: "complaints.manage",
      },
      {
        id: "delivery",
        label: "Delivery",
        href: "/delivery",
        icon: <Truck aria-hidden="true" />,
        perm: "delivery.manage",
      },
      {
        id: "inventory",
        label: "Inventory",
        href: "/inventory",
        icon: <Warehouse aria-hidden="true" />,
        perm: "inventory.view",
      },
    ],
  },
  {
    id: "catalog",
    label: "Catalog",
    items: [
      {
        id: "products",
        label: "Products",
        href: "/products",
        icon: <Package aria-hidden="true" />,
        perm: "catalog.manage",
      },
      {
        id: "pricing",
        label: "Pricing",
        href: "/pricing",
        icon: <Tag aria-hidden="true" />,
        perm: "pricing.manage",
      },
      {
        id: "promotions",
        label: "Promotions",
        href: "/promotions",
        icon: <TicketPercent aria-hidden="true" />,
        perm: "promotions.manage",
      },
    ],
  },
  {
    id: "business",
    label: "Business",
    items: [
      {
        id: "cities",
        label: "Cities & Locations",
        href: "/cities",
        icon: <MapPinned aria-hidden="true" />,
        perm: "locations.manage",
        also: ["/locations", "/service-areas"],
      },
      {
        id: "customers",
        label: "Customers",
        href: "/customers",
        icon: <Users aria-hidden="true" />,
        perm: "customers.view",
      },
      {
        id: "reports",
        label: "Reports",
        href: "/reports",
        icon: <BarChart3 aria-hidden="true" />,
        perm: "reports.view",
      },
    ],
  },
  {
    id: "finance",
    label: "Payments",
    items: [
      {
        id: "payments",
        label: "Payments",
        href: "/payments",
        icon: <ReceiptIndianRupee aria-hidden="true" />,
        perm: "payments.view",
      },
      {
        id: "refunds",
        label: "Refunds",
        href: "/refunds",
        icon: <Undo2 aria-hidden="true" />,
        perm: "payments.view",
      },
      {
        id: "cash-settlements",
        label: "Cash settlements",
        href: "/cash-settlements",
        icon: <Banknote aria-hidden="true" />,
        perm: "cash.settle",
      },
      {
        id: "payment-issues",
        label: "Payment issues",
        href: "/payment-issues",
        icon: <AlertTriangle aria-hidden="true" />,
        perm: "payments.view",
      },
    ],
  },
  {
    id: "settings",
    label: "Settings",
    items: [
      {
        id: "staff",
        label: "Staff & roles",
        href: "/settings/staff",
        icon: <UserCog aria-hidden="true" />,
        perm: "staff.manage",
      },
      {
        id: "business-profile",
        label: "Business profile",
        href: "/settings/business-profile",
        icon: <Building2 aria-hidden="true" />,
        perm: "settings.manage",
      },
      {
        id: "order-policies",
        label: "Order policies",
        href: "/settings/order-policies",
        icon: <ClipboardList aria-hidden="true" />,
        perm: "settings.manage",
      },
      {
        id: "number-series",
        label: "Number series",
        href: "/settings/number-series",
        icon: <Hash aria-hidden="true" />,
        perm: "settings.manage",
      },
      {
        id: "notification-templates",
        label: "Notification templates",
        href: "/settings/notification-templates",
        icon: <Bell aria-hidden="true" />,
        perm: "notifications.manage",
      },
      {
        id: "audit",
        label: "Audit log",
        href: "/settings/audit",
        icon: <ScrollText aria-hidden="true" />,
        perm: "audit.view",
      },
      {
        id: "account",
        label: "My account",
        href: "/account",
        icon: <Settings2 aria-hidden="true" />,
      },
    ],
  },
];

/** Groups with only the entries this staff member may open; empty groups disappear. */
export function visibleNavigation(
  can: (perm: PermissionRequirement | undefined) => boolean,
): NavGroup[] {
  return NAVIGATION.map((g) => ({ ...g, items: g.items.filter((i) => can(i.perm)) })).filter(
    (g) => g.items.length > 0,
  );
}

/** The nav entry whose href is the longest prefix of the path ("/" matches only itself). */
export function activeEntryId(pathname: string): string | undefined {
  let best: NavEntry | undefined;
  for (const group of NAVIGATION) {
    for (const item of group.items) {
      const match =
        item.href === "/"
          ? pathname === "/"
          : [item.href, ...(item.also ?? [])].some(
              (href) => pathname === href || pathname.startsWith(`${href}/`),
            );
      if (match && (!best || item.href.length > best.href.length)) best = item;
    }
  }
  return best?.id;
}
