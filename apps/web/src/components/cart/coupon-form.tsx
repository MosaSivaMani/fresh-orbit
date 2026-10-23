"use client";

import { Button, FormField, Input } from "@fresh/ui";
import { TicketPercent } from "lucide-react";
import Link from "next/link";
import { type FormEvent, useState } from "react";
import { type CartCoupon } from "@/api/types/cart";
import { useSession } from "@/auth/use-session";
import { useApplyCoupon, useRemoveCoupon } from "@/cart/use-cart";
import { describeError, fieldMessage, hasCode } from "@/lib/errors";

/** Apply or remove a coupon. Validity and discount are the server's (CART-R23); guests are asked to sign in. */
export function CouponForm({ coupon }: { coupon: CartCoupon | null }) {
  const { status } = useSession();
  const apply = useApplyCoupon();
  const remove = useRemoveCoupon();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);

  if (status !== "signedIn") {
    return (
      <p className="text-text-muted text-sm">
        Have a coupon?{" "}
        <Link
          href="/login?next=%2Fcart"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
        >
          Sign in
        </Link>{" "}
        to apply it.
      </p>
    );
  }

  if (coupon) {
    const applied = coupon.status === "APPLIED";
    return (
      <div
        className={
          applied
            ? "bg-success-subtle flex items-start justify-between gap-3 rounded-lg p-3"
            : "bg-highlight-subtle flex items-start justify-between gap-3 rounded-lg p-3"
        }
      >
        <div className="flex flex-col gap-0.5 text-sm">
          <span className="text-text flex items-center gap-1.5 font-semibold">
            <TicketPercent aria-hidden="true" className="size-4" />
            {coupon.code}
          </span>
          {coupon.message && (
            <span className={applied ? "text-success-text" : "text-highlight-text"}>
              {coupon.message}
            </span>
          )}
        </div>
        <Button
          variant="ghost"
          size="sm"
          loading={remove.isPending}
          onClick={() => remove.mutate(undefined, { onError: (e) => setError(describeError(e)) })}
        >
          Remove
        </Button>
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
    <form onSubmit={(e) => void onSubmit(e)} className="flex items-end gap-2" noValidate>
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
