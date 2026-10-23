"use client";

import { Button, Card, CardContent, EmptyState, ErrorState, Skeleton } from "@fresh/ui";
import { ShoppingBag } from "lucide-react";
import Link from "next/link";
import { useSession } from "@/auth/use-session";
import { useCart } from "@/cart/use-cart";
import { ChangePincodeButton } from "@/components/change-pincode-button";
import { asApiError, describeError } from "@/lib/errors";
import { useLocation } from "@/location/location-context";
import { CartLineRow } from "./cart-line-row";
import { CouponForm } from "./coupon-form";
import { OrderSummary } from "./order-summary";

function CartSkeleton() {
  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_22rem]" aria-busy="true">
      <span className="sr-only" role="status">
        Loading your cart
      </span>
      <div className="flex flex-col gap-4">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-28 rounded-xl" />
        ))}
      </div>
      <Skeleton className="h-72 rounded-xl" />
    </div>
  );
}

/** The cart exactly as the server priced it (CART-R1); every change returns a newly priced cart. */
export function CartView() {
  const cart = useCart();
  const { status } = useSession();
  const { pincode } = useLocation();

  if (cart.isPending) return <CartSkeleton />;
  if (cart.isError) {
    return (
      <ErrorState
        title="We could not load your cart"
        description={describeError(cart.error)}
        traceId={asApiError(cart.error)?.traceId}
        onRetry={() => void cart.refetch()}
      />
    );
  }

  const data = cart.data;
  if (data.lines.length === 0) {
    return (
      <EmptyState
        icon={<ShoppingBag aria-hidden="true" />}
        title="Your cart is empty"
        description="Browse today's catch and add what you would like delivered."
        action={
          <Button asChild>
            <Link href="/shop">Start shopping</Link>
          </Button>
        }
      />
    );
  }

  const checkoutHref = status === "signedIn" ? "/checkout" : "/login?next=%2Fcheckout";
  const locationDiffers = pincode !== null && data.pincode !== null && pincode !== data.pincode;

  return (
    <div className="grid items-start gap-8 lg:grid-cols-[1fr_22rem]">
      <div className="flex flex-col gap-4">
        {data.pincode && (
          <div className="border-border bg-surface flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm">
            <span className="text-text">
              Priced for delivery to <span className="font-semibold">{data.pincode}</span>
              {locationDiffers && (
                <span className="text-highlight-text">
                  {" "}
                  (you are browsing {pincode}; change your pincode to move the cart)
                </span>
              )}
            </span>
            <ChangePincodeButton label="Change" variant="ghost" size="sm" />
          </div>
        )}
        {data.messages.length > 0 && (
          <ul className="bg-info-subtle text-info-text flex flex-col gap-1 rounded-xl px-4 py-3 text-sm">
            {data.messages.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}
        <Card>
          <CardContent>
            <ul aria-label="Items in your cart">
              {data.lines.map((line) => (
                <CartLineRow key={line.id} line={line} />
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>

      <Card className="lg:sticky lg:top-24">
        <CardContent className="flex flex-col gap-5">
          <h2 className="text-text text-lg font-semibold">Order summary</h2>
          <CouponForm coupon={data.coupon} />
          <OrderSummary totals={data.totals} coupon={data.coupon} />
          {data.checkoutBlockers.length > 0 && (
            <ul className="flex flex-col gap-1" aria-label="Before you check out">
              {data.checkoutBlockers.map((b) => (
                <li
                  key={b.code}
                  className="bg-warning-subtle text-warning-text rounded-md px-3 py-2 text-sm"
                >
                  {b.message}
                </li>
              ))}
            </ul>
          )}
          {data.canCheckout ? (
            <Button asChild size="lg" className="w-full">
              <Link href={checkoutHref}>
                {status === "signedIn" ? "Proceed to checkout" : "Sign in to check out"}
              </Link>
            </Button>
          ) : (
            <Button size="lg" className="w-full" disabled>
              Proceed to checkout
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
