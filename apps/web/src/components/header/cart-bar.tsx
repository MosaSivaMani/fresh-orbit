"use client";

import { formatRupees } from "@fresh/core";
import { FloatingCartBar } from "@fresh/ui";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCart } from "@/cart/use-cart";

/** Browsing pages where the floating "View cart" bar appears once the cart has items. */
function showsCartBar(pathname: string): boolean {
  return (
    pathname === "/" ||
    pathname === "/shop" ||
    pathname.startsWith("/shop/") ||
    pathname === "/search"
  );
}

/**
 * The floating teal cart bar (D88). The count is the number of lines the server returned and the amount is the
 * server's estimated total; nothing is added up here (CLAUDE.md rule 4).
 */
export function CartBar() {
  const pathname = usePathname();
  const cart = useCart();
  const lines = cart.data?.lines.length ?? 0;
  if (!cart.data || lines === 0 || !showsCartBar(pathname)) return null;
  return (
    <FloatingCartBar
      summary={`${lines} ${lines === 1 ? "item" : "items"} in your cart`}
      total={`${formatRupees(cart.data.totals.estimatedTotalPaise)} estimated`}
      href="/cart"
      renderLink={({ href, className, children }) => (
        <Link href={href} className={className}>
          {children}
        </Link>
      )}
    />
  );
}
