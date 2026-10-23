"use client";

import { Bell, Heart, MapPin, Package, User } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cx } from "@/lib/cx";

const LINKS = [
  { href: "/account", label: "Profile", icon: User },
  { href: "/account/orders", label: "Orders", icon: Package },
  { href: "/account/addresses", label: "Addresses", icon: MapPin },
  { href: "/account/wishlist", label: "Saved items", icon: Heart },
  { href: "/account/notifications", label: "Notifications", icon: Bell },
] as const;

export function AccountNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Account">
      <ul className="flex gap-1 overflow-x-auto md:flex-col">
        {LINKS.map(({ href, label, icon: Icon }) => {
          const active = href === "/account" ? pathname === href : pathname.startsWith(href);
          return (
            <li key={href} className="shrink-0">
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cx(
                  "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium",
                  active ? "bg-accent-subtle text-accent-text" : "text-text hover:bg-muted",
                )}
              >
                <Icon aria-hidden="true" className="size-4" />
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
