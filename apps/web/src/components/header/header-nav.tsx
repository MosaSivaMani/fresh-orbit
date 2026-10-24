"use client";

import { cn, focusRing } from "@fresh/ui";
import { BadgePercent, LifeBuoy, ReceiptText, ShoppingCart, UserRound } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { type ReactNode } from "react";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";

/** Whether `href` is the current section; the home page only matches itself. */
export function isCurrent(pathname: string, href: string): boolean {
  const path = href.split(/[?#]/)[0] ?? href;
  if (path === "/") return pathname === "/";
  return pathname === path || pathname.startsWith(`${path}/`);
}

function NavLink({
  href,
  icon,
  label,
  current,
}: {
  href: string;
  icon: ReactNode;
  label: string;
  current: boolean;
}) {
  return (
    <Link
      href={href}
      aria-current={current ? "page" : undefined}
      className={cn(
        "flex items-center gap-1.5 rounded-lg px-1.5 py-1 text-[14.5px] font-semibold underline-offset-[6px] hover:underline [&_svg]:size-[18px]",
        current && "underline decoration-2",
        focusRing,
      )}
    >
      {icon}
      {/* Phones keep the icons; the word shows from sm up (and is always the link's name). */}
      <span className="sr-only sm:not-sr-only">{label}</span>
    </Link>
  );
}

/**
 * The coral header's links (D95): Offers, Help, Orders, Sign in or Account, and the ink cart pill. The cart count is
 * the number of lines in the server's cart.
 */
export function HeaderNav() {
  const pathname = usePathname();
  const { status } = useSession();
  const cart = useCart();
  const signedIn = status === "signedIn";
  const count = cart.data?.lines.length ?? 0;
  return (
    <nav aria-label="Main" className="flex items-center gap-1 sm:gap-4">
      <NavLink
        href="/#offers"
        icon={<BadgePercent aria-hidden="true" />}
        label="Offers"
        current={false}
      />
      <NavLink
        href="/support"
        icon={<LifeBuoy aria-hidden="true" />}
        label="Help"
        current={isCurrent(pathname, "/support")}
      />
      {signedIn && (
        <NavLink
          href="/account/orders"
          icon={<ReceiptText aria-hidden="true" />}
          label="Orders"
          current={isCurrent(pathname, "/account/orders") || isCurrent(pathname, "/orders")}
        />
      )}
      <NavLink
        href={signedIn ? "/account" : "/login"}
        icon={<UserRound aria-hidden="true" />}
        label={signedIn ? "Account" : "Sign in"}
        current={
          signedIn
            ? isCurrent(pathname, "/account") && !isCurrent(pathname, "/account/orders")
            : isCurrent(pathname, "/login")
        }
      />
      <Link
        href="/cart"
        aria-current={isCurrent(pathname, "/cart") ? "page" : undefined}
        className={cn(
          "bg-outline-ink text-on-outline-ink ml-1 flex items-center gap-1.5 rounded-xl px-3 py-2 text-sm font-semibold [&_svg]:size-[18px]",
          focusRing,
        )}
      >
        <ShoppingCart aria-hidden="true" />
        <span>Cart</span>
        {count > 0 && (
          <>
            <span
              aria-hidden="true"
              className="bg-accent-coral text-on-accent-coral grid h-[18px] min-w-[18px] place-items-center rounded-full px-1 text-[11px] font-bold"
            >
              {count}
            </span>
            <span className="sr-only">{` (${count} in cart)`}</span>
          </>
        )}
      </Link>
    </nav>
  );
}
