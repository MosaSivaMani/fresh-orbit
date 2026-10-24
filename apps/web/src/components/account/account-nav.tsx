"use client";

import { FilterChip } from "@fresh/ui";
import { Bell, Heart, MapPin, Package, Settings, User } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

const LINKS = [
  { href: "/account", label: "Profile", icon: User },
  { href: "/account/orders", label: "Orders", icon: Package },
  { href: "/account/addresses", label: "Addresses", icon: MapPin },
  { href: "/account/wishlist", label: "Saved items", icon: Heart },
  { href: "/account/notifications", label: "Notifications", icon: Bell },
  { href: "/account/settings", label: "Settings", icon: Settings },
] as const;

/** Account sections as a row of food-app chips, the current one filled with ink; the same row at every width. */
export function AccountNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Account">
      <ul className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:px-0">
        {LINKS.map(({ href, label, icon: Icon }) => {
          const active = href === "/account" ? pathname === href : pathname.startsWith(href);
          return (
            <li key={href} className="shrink-0">
              <FilterChip asChild pressed={active}>
                <Link href={href} aria-current={active ? "page" : undefined}>
                  <Icon aria-hidden="true" />
                  {label}
                </Link>
              </FilterChip>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
