"use client";

import { formatRupees } from "@fresh/core";
import { Button, Skeleton, StepCard, StickyActionBar } from "@fresh/ui";
import { MapPin } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";
import { ChangePincodeButton } from "@/components/change-pincode-button";
import { SlotPicker } from "@/components/checkout/slot-picker";
import { EmptyPage, LoadError } from "@/components/states";
import { useLocation } from "@/location/location-context";
import { CartLineRow } from "./cart-line-row";
import { CouponForm } from "./coupon-form";
import { OrderSummary } from "./order-summary";

function CartSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_24rem]" aria-busy="true">
      <span className="sr-only" role="status">
        Loading your cart
      </span>
      <div className="flex flex-col gap-4">
        <Skeleton className="h-64 rounded-[22px]" />
        <Skeleton className="h-36 rounded-[22px]" />
      </div>
      <Skeleton className="h-72 rounded-[22px]" />
    </div>
  );
}

/** Where Place order goes: checkout with the chosen slot, through sign-in for a guest. */
export function checkoutHref(signedIn: boolean, slotId: string | null): string {
  const target = slotId ? `/checkout?slot=${encodeURIComponent(slotId)}` : "/checkout";
  return signedIn ? target : `/login?next=${encodeURIComponent(target)}`;
}

/**
 * The cart exactly as the server priced it (CART-R1), in the food-app look (D88/D95): item rows with a − n + stepper,
 * the coupon strip, slot buttons, the bill box and a sticky Place order bar. Every change returns a newly priced cart.
 */
export function CartView() {
  const cart = useCart();
  const { status } = useSession();
  const { pincode } = useLocation();
  const [slotId, setSlotId] = useState<string | null>(null);

  if (cart.isPending) return <CartSkeleton />;
  if (cart.isError) {
    return (
      <LoadError
        title="We could not load your cart"
        error={cart.error}
        onRetry={() => void cart.refetch()}
      />
    );
  }

  const data = cart.data;
  if (data.lines.length === 0) {
    return (
      <EmptyPage
        slot="EMPTY_CART"
        title="Your cart is empty"
        description="Browse today's catch and add what you would like delivered."
        actions={
          <Button asChild size="lg">
            <Link href="/shop">Start shopping</Link>
          </Button>
        }
      />
    );
  }

  const signedIn = status === "signedIn";
  const locationDiffers = pincode !== null && data.pincode !== null && pincode !== data.pincode;
  const itemCount = data.lines.length;

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[1fr_24rem]">
      <div className="flex min-w-0 flex-col gap-4">
        {data.pincode && (
          <div className="bg-surface-sand flex flex-wrap items-center justify-between gap-3 rounded-2xl px-4 py-3 text-sm">
            <span className="text-text flex items-center gap-2">
              <MapPin aria-hidden="true" className="text-accent-coral-text size-4 shrink-0" />
              <span>
                Priced for delivery to <span className="font-bold">{data.pincode}</span>
                {locationDiffers && (
                  <span className="text-highlight-text">
                    {" "}
                    (you are browsing {pincode}; change your pincode to move the cart)
                  </span>
                )}
              </span>
            </span>
            <ChangePincodeButton label="Change" variant="ghost" size="sm" />
          </div>
        )}
        {data.messages.length > 0 && (
          <ul className="bg-info-subtle text-info-text flex flex-col gap-1 rounded-2xl px-4 py-3 text-sm">
            {data.messages.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}
        <StepCard title={itemCount === 1 ? "1 item" : `${itemCount} items`}>
          <ul aria-label="Items in your cart">
            {data.lines.map((line) => (
              <CartLineRow key={line.id} line={line} />
            ))}
          </ul>
        </StepCard>
        {data.pincode && (
          <StepCard title="Delivery slot">
            <SlotPicker pincode={data.pincode} value={slotId} onChange={setSlotId} />
            <p className="text-text-muted text-xs">
              You can change the slot and the address at checkout.
            </p>
          </StepCard>
        )}
      </div>

      <div className="flex flex-col gap-4 lg:sticky lg:top-20">
        <CouponForm coupon={data.coupon} />
        <OrderSummary totals={data.totals} coupon={data.coupon} />
        {data.checkoutBlockers.length > 0 && (
          <ul className="flex flex-col gap-1" aria-label="Before you check out">
            {data.checkoutBlockers.map((b) => (
              <li
                key={`${b.code}-${b.lineId ?? ""}`}
                className="bg-warning-subtle text-warning-text rounded-xl px-3 py-2 text-sm"
              >
                {b.message}
              </li>
            ))}
          </ul>
        )}
      </div>

      <StickyActionBar
        className="lg:col-span-2"
        summary="Estimated total"
        amount={formatRupees(data.totals.estimatedTotalPaise)}
        action={
          data.canCheckout ? (
            <Button asChild size="lg">
              <Link href={checkoutHref(signedIn, slotId)}>
                {signedIn ? "Place order" : "Sign in to place order"}
              </Link>
            </Button>
          ) : (
            <Button size="lg" disabled>
              Place order
            </Button>
          )
        }
      />
    </div>
  );
}
