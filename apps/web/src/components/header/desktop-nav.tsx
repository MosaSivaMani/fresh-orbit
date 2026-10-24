"use client";

import { cn } from "@fresh/ui";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { currentTab, useNavItems } from "./tab-bar";

/**
 * From md up the phone tab bar becomes this row in the top header: the same five items, icon beside label, the
 * current one in a teal pill and the cart count in a coral badge.
 */
export function DesktopNav() {
  const pathname = usePathname();
  const items = useNavItems();
  const current = currentTab(
    pathname,
    items.map((i) => i.href),
  );
  return (
    <nav aria-label="Main" className="hidden items-center gap-1 md:flex">
      {items.map((item) => {
        const active = item.href === current;
        return (
          <Link
            key={item.label}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "relative inline-flex h-10 items-center gap-2 rounded-full px-3 text-sm [&_svg]:size-[18px]",
              "focus-visible:outline-ring outline-none focus-visible:outline-2 focus-visible:outline-offset-2",
              active
                ? "bg-teal-soft text-accent-text font-extrabold"
                : "text-text hover:bg-muted font-semibold",
            )}
          >
            <span aria-hidden="true">{item.icon}</span>
            {item.label}
            {item.badge != null && item.badge > 0 && (
              <>
                <span
                  aria-hidden="true"
                  className="bg-accent-coral text-on-accent-coral min-w-5 rounded-full px-1.5 text-center text-xs leading-5 font-extrabold"
                >
                  {item.badge}
                </span>
                <span className="sr-only">{` (${item.badge} in cart)`}</span>
              </>
            )}
          </Link>
        );
      })}
    </nav>
  );
}
