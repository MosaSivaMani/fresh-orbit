"use client";

import { cn, focusRing } from "@fresh/ui";
import { BadgePercent, Headphones, ReceiptText, ShoppingCart, UserRound } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { browserHttp } from "@/api/browser";
import { getPromotions } from "@/api/endpoints/catalog";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";
import { queryKeys } from "@/lib/query-keys";
import { useLocation } from "@/location/location-context";

/** Whether `href` is the current section; the home page only matches itself. */
export function isCurrent(pathname: string, href: string): boolean {
  const path = href.split(/[?#]/)[0] ?? href;
  if (path === "/") return pathname === "/";
  return pathname === path || pathname.startsWith(`${path}/`);
}

/**
 * Whether the home shows its "Today's offers" section (`/#offers`): the server has at least one banner for the
 * visitor's pincode. Until that is known, and when it fails, the Offers link stays hidden, never a dead anchor.
 */
function useHasOffers(): boolean {
  const { pincode, ready } = useLocation();
  const promotions = useQuery({
    queryKey: queryKeys.promotions(pincode),
    queryFn: () => getPromotions(browserHttp()),
    enabled: ready,
    staleTime: 300_000,
  });
  return (promotions.data?.length ?? 0) > 0;
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
 * The header's links (D188): Offers, Help, Orders, Sign in or Account, and the dark teal cart pill. The cart count is
 * the number of lines in the server's cart.
 */
export function HeaderNav() {
  const pathname = usePathname();
  const { status } = useSession();
  const cart = useCart();
  const signedIn = status === "signedIn";
  const count = cart.data?.lines.length ?? 0;
  const hasOffers = useHasOffers();
  return (
    <nav aria-label="Main" className="flex items-center gap-1 sm:gap-4">
      {hasOffers && (
        <NavLink
          href="/#offers"
          icon={<BadgePercent aria-hidden="true" className="text-accent-coral" />}
          label="Offers"
          current={false}
        />
      )}
      <NavLink
        href="/support"
        icon={<Headphones aria-hidden="true" />}
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
          "bg-primary text-on-primary hover:bg-primary-hover ml-1 flex items-center gap-2 rounded-full px-4 py-2.5 text-sm font-bold [&_svg]:size-[18px]",
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
