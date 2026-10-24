"use client";

import { Button, CouponStrip, FormField, Input } from "@fresh/ui";
import { TicketPercent } from "lucide-react";
import Link from "next/link";
import { type FormEvent, useState } from "react";
import { type CartCoupon } from "@/api/types/cart";
import { useSession } from "@/auth/use-session";
import { useApplyCoupon, useRemoveCoupon } from "@/cart/use-cart";
import { describeError, fieldMessage, hasCode } from "@/lib/errors";

const ticket = <TicketPercent />;

/**
 * The coupon strip (D88): apply or remove a coupon. Validity and discount are the server's (CART-R23); the strip
 * shows the server's message as given. Guests are asked to sign in.
 */
export function CouponForm({ coupon }: { coupon: CartCoupon | null }) {
  const { status } = useSession();
  const apply = useApplyCoupon();
  const remove = useRemoveCoupon();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);

  if (status !== "signedIn") {
    return (
      <CouponStrip
        icon={ticket}
        title="Have a coupon?"
        description="Sign in to apply it."
        action={
          <Button asChild variant="outline" size="sm">
            <Link href="/login?next=%2Fcart">Sign in</Link>
          </Button>
        }
      />
    );
  }

  if (coupon) {
    return (
      <div className="flex flex-col gap-1.5">
        <CouponStrip
          icon={ticket}
          title={coupon.code}
          description={
            coupon.message && (
              <span
                className={
                  coupon.status === "APPLIED" ? "text-success-text" : "text-highlight-text"
                }
              >
                {coupon.message}
              </span>
            )
          }
          action={
            <Button
              variant="ghost"
              size="sm"
              loading={remove.isPending}
              aria-label={`Remove coupon ${coupon.code}`}
              onClick={() =>
                remove.mutate(undefined, { onError: (e) => setError(describeError(e)) })
              }
            >
              Remove
            </Button>
          }
        />
        {error && (
          <p role="alert" className="text-danger-text text-sm">
            {error}
          </p>
        )}
      </div>
    );
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = code.trim();
    if (!trimmed) {
      setError("Enter a coupon code.");
      return;
    }
    setError(null);
    try {
      await apply.mutateAsync(trimmed);
      setCode("");
    } catch (e) {
      setError(
        hasCode(e, "LOGIN_REQUIRED")
          ? "Sign in to apply a coupon."
          : (fieldMessage(e, "code") ?? describeError(e)),
      );
    }
  }

  return (
    <form
      onSubmit={(e) => void onSubmit(e)}
      noValidate
      className="border-mango bg-mango-soft flex items-end gap-2 rounded-[14px] border-[1.5px] border-dashed p-3"
    >
      <FormField label="Coupon code" error={error ?? undefined} className="flex-1">
        <Input
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          maxLength={40}
        />
      </FormField>
      <Button type="submit" variant="outline" loading={apply.isPending}>
        Apply
      </Button>
    </form>
  );
}
