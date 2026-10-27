"use client";

import { formatDate, formatRupees } from "@fresh/core";
import { Button, CouponStrip, FormField, Input, Skeleton } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { TicketPercent } from "lucide-react";
import Link from "next/link";
import { type FormEvent, useState } from "react";
import { browserHttp } from "@/api/browser";
import { listCoupons } from "@/api/endpoints/account";
import { type CartCoupon } from "@/api/types/cart";
import { useSession } from "@/auth/use-session";
import { useApplyCoupon, useRemoveCoupon } from "@/cart/use-cart";
import { describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const ticket = <TicketPercent />;

/**
 * "View offers": the coupons the server shows this customer (`GET /coupons`), each with its own description, the
 * server's minimum order and end date, and an Apply button. Whether it applies is the server's answer.
 */
function CouponOffers({
  onApply,
  applying,
}: {
  onApply: (code: string) => void;
  applying: string | null;
}) {
  const coupons = useQuery({
    queryKey: queryKeys.coupons,
    queryFn: () => listCoupons(browserHttp()),
  });
  if (coupons.isPending) return <Skeleton className="h-16 rounded-[16px]" />;
  if (coupons.isError) {
    return (
      <p role="alert" className="text-danger-text text-sm">
        {describeError(coupons.error)}
      </p>
    );
  }
  if (coupons.data.length === 0) {
    return <p className="text-text-muted text-sm">No offers for you right now.</p>;
  }
  return (
    <ul aria-label="Available offers" className="flex flex-col gap-2">
      {coupons.data.map((c) => (
        <li
          key={c.code}
          className="bg-surface-paper border-border flex items-center justify-between gap-3 rounded-[16px] border p-3 text-sm"
        >
          <div className="min-w-0">
            <p className="text-text font-bold tracking-[0.04em]">{c.code}</p>
            {c.description && <p className="text-text-muted">{c.description}</p>}
            <p className="text-text-muted text-xs">
              {c.minOrderPaise > 0 && `Orders from ${formatRupees(c.minOrderPaise)}. `}
              {c.firstOrderOnly && "First order only. "}
              {c.validTo && `Valid until ${formatDate(c.validTo)}.`}
            </p>
          </div>
          <Button
            size="sm"
            variant="outline"
            loading={applying === c.code}
            disabled={applying !== null}
            onClick={() => onApply(c.code)}
            aria-label={`Apply ${c.code}`}
          >
            Apply
          </Button>
        </li>
      ))}
    </ul>
  );
}

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
  const [showOffers, setShowOffers] = useState(false);
  const [applying, setApplying] = useState<string | null>(null);

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

  async function applyOffer(offer: string) {
    setError(null);
    setApplying(offer);
    try {
      await apply.mutateAsync(offer);
      setShowOffers(false);
    } catch (e) {
      setError(fieldMessage(e, "code") ?? describeError(e));
    } finally {
      setApplying(null);
    }
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
    <div className="flex flex-col gap-2">
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
        <Button type="submit" variant="outline" loading={apply.isPending && applying === null}>
          Apply
        </Button>
      </form>
      <Button
        variant="link"
        size="sm"
        className="self-start"
        aria-expanded={showOffers}
        onClick={() => setShowOffers((open) => !open)}
      >
        {showOffers ? "Hide offers" : "View offers"}
      </Button>
      {showOffers && <CouponOffers onApply={(c) => void applyOffer(c)} applying={applying} />}
    </div>
  );
}
