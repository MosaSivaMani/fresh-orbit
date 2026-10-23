"use client";

import { Badge, Button } from "@fresh/ui";
import { ChevronDown, MapPin, ShoppingBag, User } from "lucide-react";
import Link from "next/link";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";
import { useLocation } from "@/location/location-context";

export function PincodeChip() {
  const { pincode, ready, setPickerOpen } = useLocation();
  return (
    <Button
      variant="ghost"
      size="sm"
      className="text-text h-auto max-w-[11rem] flex-col items-start gap-0 px-2 py-1 text-left"
      onClick={() => setPickerOpen(true)}
      aria-label={pincode ? `Delivering to ${pincode}. Change pincode` : "Set delivery pincode"}
    >
      <span className="text-text-muted text-xs font-normal">
        {pincode ? "Delivering to" : "Deliver to"}
      </span>
      <span className="flex items-center gap-1 text-sm font-semibold">
        <MapPin aria-hidden="true" className="size-3.5" />
        {ready ? (pincode ?? "Set pincode") : "…"}
        <ChevronDown aria-hidden="true" className="size-3.5" />
      </span>
    </Button>
  );
}

export function CartLink() {
  const cart = useCart();
  const count = cart.data?.lines.length ?? 0;
  return (
    <Link
      href="/cart"
      className="text-text hover:bg-muted relative inline-flex size-10 items-center justify-center rounded-md"
      aria-label={count > 0 ? `Cart, ${count} ${count === 1 ? "item" : "items"}` : "Cart"}
    >
      <ShoppingBag aria-hidden="true" className="size-5" />
      {count > 0 && (
        <Badge tone="highlight" className="absolute -top-1 -right-1 px-1.5" aria-hidden="true">
          {count}
        </Badge>
      )}
    </Link>
  );
}

export function AccountLink() {
  const { status } = useSession();
  const signedIn = status === "signedIn";
  return (
    <Link
      href={signedIn ? "/account" : "/login"}
      className="text-text hover:bg-muted inline-flex h-10 items-center gap-2 rounded-md px-2 text-sm font-medium"
    >
      <User aria-hidden="true" className="size-5" />
      <span className="hidden sm:inline">{signedIn ? "Account" : "Sign in"}</span>
      <span className="sr-only sm:hidden">{signedIn ? "Account" : "Sign in"}</span>
    </Link>
  );
}
