"use client";

import { TabBar, type TabBarItem } from "@fresh/ui";
import { Home, LayoutGrid, ReceiptText, ShoppingCart, UserRound } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";

/** Pages with their own sticky action bar (add to cart, place order) hide the tab bar on phones. */
const HIDDEN_ON = ["/products/", "/checkout", "/login"];

/** Which tab a path belongs to; the longest matching prefix wins (Orders lives under /account). */
export function currentTab(pathname: string, hrefs: readonly string[]): string | undefined {
  if (pathname === "/") return "/";
  return hrefs
    .filter((h) => h !== "/" && (pathname === h || pathname.startsWith(`${h}/`)))
    .sort((a, b) => b.length - a.length)[0];
}

/** Navigation items shared by the phone tab bar and the desktop header (D88). */
export function useNavItems(): TabBarItem[] {
  const { status } = useSession();
  const cart = useCart();
  const signedIn = status === "signedIn";
  return [
    { href: "/", label: "Home", icon: <Home /> },
    { href: "/shop", label: "Shop", icon: <LayoutGrid /> },
    { href: "/cart", label: "Cart", icon: <ShoppingCart />, badge: cart.data?.lines.length ?? 0 },
    {
      href: signedIn ? "/account/orders" : "/login?next=%2Faccount%2Forders",
      label: "Orders",
      icon: <ReceiptText />,
    },
    { href: signedIn ? "/account" : "/login", label: "Account", icon: <UserRound /> },
  ];
}

/** The phone tab bar: Home, Shop, Cart, Orders, Account. From md up the header shows the same items. */
export function AppTabBar() {
  const pathname = usePathname();
  const items = useNavItems();
  if (HIDDEN_ON.some((p) => pathname.startsWith(p))) return null;
  return (
    <TabBar
      items={items}
      current={currentTab(
        pathname,
        items.map((i) => i.href),
      )}
      renderLink={({ href, className, children, ...rest }) => (
        <Link href={href} className={className} {...rest}>
          {children}
        </Link>
      )}
    />
  );
}
