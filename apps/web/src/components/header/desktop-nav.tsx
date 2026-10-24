"use client";

import { cn } from "@fresh/ui";
import { ShoppingCart, UserRound } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";

const focus =
  "outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

/**
 * Desktop header actions (web mockup `docs/design/mockups/web-bold-blocks.html`): Shop, Offers and Orders links,
 * a teal Cart button with the server's line count, and the account circle. Phones use the bottom tab bar instead.
 */
export function DesktopNav() {
  const pathname = usePathname();
  const { status } = useSession();
  const cart = useCart();
  const signedIn = status === "signedIn";
  const count = cart.data?.lines.length ?? 0;
  const links = [
    { href: "/shop", label: "Shop", active: pathname === "/shop" || pathname.startsWith("/shop/") },
    { href: "/#offers", label: "Offers", active: false },
    {
      href: signedIn ? "/account/orders" : "/login?next=%2Faccount%2Forders",
      label: "Orders",
      active: pathname.startsWith("/account/orders") || pathname.startsWith("/orders/"),
    },
  ];
  return (
    <div className="hidden items-center gap-3 md:flex">
      <nav aria-label="Main" className="flex items-center gap-1">
        {links.map((link) => (
          <Link
            key={link.label}
            href={link.href}
            aria-current={link.active ? "page" : undefined}
            className={cn(
              "rounded-full px-3 py-2 text-sm font-semibold",
              link.active ? "bg-teal-soft text-accent-text" : "text-text hover:bg-muted",
              focus,
            )}
          >
            {link.label}
          </Link>
        ))}
      </nav>
      <Link
        href="/cart"
        aria-current={pathname === "/cart" ? "page" : undefined}
        className={cn(
          "bg-accent text-on-accent hover:bg-accent-hover relative inline-flex h-10 items-center gap-1.5 rounded-[14px] px-3.5 text-sm font-bold",
          focus,
        )}
      >
        <ShoppingCart aria-hidden="true" className="size-[18px]" />
        Cart
        {count > 0 && (
          <>
            <span
              aria-hidden="true"
              className="border-surface-paper bg-accent-coral text-on-accent-coral absolute -top-2 -right-2 grid h-5 min-w-5 place-items-center rounded-full border-2 px-1 text-[0.6875rem] font-extrabold"
            >
              {count}
            </span>
            <span className="sr-only">{` (${count} in cart)`}</span>
          </>
        )}
      </Link>
      <Link
        href={signedIn ? "/account" : "/login"}
        aria-label={signedIn ? "Account" : "Sign in"}
        title={signedIn ? "Account" : "Sign in"}
        className={cn(
          "bg-mango-soft text-text grid size-10 place-items-center rounded-full",
          pathname.startsWith("/account") && !pathname.startsWith("/account/orders") && "ring-outline-ink ring-2",
          focus,
        )}
      >
        <UserRound aria-hidden="true" className="size-5" />
      </Link>
    </div>
  );
}
