"use client";

import { formatRupees, newIdempotencyKey } from "@fresh/core";
import {
  Button,
  Dialog,
  DialogContent,
  FormField,
  OptionTile,
  Skeleton,
  StepCard,
  StickyActionBar,
  Textarea,
} from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPinPlus, Pencil } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type ReactNode, useEffect, useId, useRef, useState } from "react";
import { browserHttp } from "@/api/browser";
import { addAddress, listAddresses, replaceAddress } from "@/api/endpoints/account";
import { previewCheckout, placeOrder } from "@/api/endpoints/orders";
import { type Cart, type DroppedCartLine } from "@/api/types/cart";
import {
  type Address,
  type AddressInput,
  type CheckoutPreview,
  type PaymentMethod,
  type SubstitutionPreference,
} from "@/api/types/checkout";
import { AddressForm } from "@/components/account/address-form";
import { OrderSummary } from "@/components/cart/order-summary";
import { EmptyPage, LoadError } from "@/components/states";
import { useCart, useChangeCartLocation } from "@/cart/use-cart";
import { asApiError, describeError, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { paymentMethodLabels } from "@/lib/status-maps";
import { useLocation } from "@/location/location-context";
import { SlotPicker } from "./slot-picker";

const SUBSTITUTIONS: { value: SubstitutionPreference; label: string; description: string }[] = [
  {
    value: "CALL_ME",
    label: "Call me",
    description: "If an item is short, we call before changing anything.",
  },
  {
    value: "AUTO_SIMILAR",
    label: "Pick something similar",
    description: "We may send a similar item and charge its actual price.",
  },
  {
    value: "NONE",
    label: "Leave it out",
    description: "We skip a short item and do not charge for it.",
  },
];

const DEFAULT_METHODS: PaymentMethod[] = ["COD", "UPI_ON_DELIVERY"];

const PAYMENT_NOTES: Record<string, string> = {
  COD: "Pay the delivery partner in cash.",
  UPI_ON_DELIVERY: "Scan the partner's QR code at your door.",
};

/** A group of option tiles with a visible legend. */
function Choices({
  legend,
  children,
  className,
}: {
  legend: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-text mb-2 text-sm font-bold">{legend}</legend>
      <div className={className ?? "grid gap-2 sm:grid-cols-2"}>{children}</div>
    </fieldset>
  );
}

function addressText(a: Address): string {
  return [a.line1, a.line2, a.landmark, a.locality, `${a.cityName} ${a.pincode}`]
    .filter(Boolean)
    .join(", ");
}

function labelText(label: string): string {
  return `${label.charAt(0)}${label.slice(1).toLowerCase()}`;
}

/** What makes the cart a different order: its lines, their quantities and preparations, and the coupon. */
function cartKeyOf(cart: Cart | undefined): string {
  if (!cart) return "";
  const lines = cart.lines.map((l) =>
    [l.id, l.variantId, l.quantity.value, l.quantity.unit, l.preparation?.code ?? ""].join(":"),
  );
  return `${lines.join("|")}#${cart.coupon?.code ?? ""}`;
}

type PriceChange = { previousTotalPaise: number; preview: CheckoutPreview };

/**
 * Checkout in the food-app look (D95): address, slot, pay on delivery (COD / UPI on delivery, as the server's
 * preview allows), preferences, the server's bill and a sticky Place order bar. `initialSlotId` is the slot chosen
 * in the cart; the server's preview decides whether it can still be used.
 */
export function CheckoutView({ initialSlotId = null }: { initialSlotId?: string | null }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const groupName = useId();
  const { choosePincode } = useLocation();
  const cart = useCart();
  const moveCart = useChangeCartLocation();
  const addresses = useQuery({
    queryKey: queryKeys.addresses,
    queryFn: () => listAddresses(browserHttp()),
  });

  const [chosenAddressId, setAddressId] = useState<string | null>(null);
  const [slotId, setSlotId] = useState<string | null>(initialSlotId);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("COD");
  const [substitution, setSubstitution] = useState<SubstitutionPreference>("CALL_ME");
  const [notes, setNotes] = useState("");
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<Address | null>(null);
  const [addressNotice, setAddressNotice] = useState<string | null>(null);
  const [dropped, setDropped] = useState<DroppedCartLine[] | null>(null);
  const [priceChange, setPriceChange] = useState<PriceChange | null>(null);
  const [placeError, setPlaceError] = useState<{ message: string; traceId?: string } | null>(null);
  // One key per "place order" action, reused while retrying the same action (docs/api §1.5).
  const placeKey = useRef<string | null>(null);

  const list = addresses.data ?? [];
  const address =
    list.find((a) => a.addressId === chosenAddressId) ??
    list.find((a) => a.isDefault) ??
    list[0] ??
    null;
  const addressId = address ? address.addressId : null;

  // A new address, slot, payment method, substitution choice or note is a new order body: a retry must not reuse
  // the old key (the server would answer IDEMPOTENCY_KEY_REUSED), so the next Place order takes a fresh one.
  // A changed cart (lines, quantities, preparations or coupon) is a new order too (D315).
  const cartFingerprint = cartKeyOf(cart.data);
  useEffect(() => {
    placeKey.current = null;
  }, [addressId, slotId, paymentMethod, substitution, notes, cartFingerprint]);

  const previewKey = ["checkout-preview", addressId, slotId, paymentMethod, substitution] as const;
  const preview = useQuery({
    queryKey: previewKey,
    queryFn: () =>
      previewCheckout(browserHttp(), {
        addressId: addressId!,
        deliverySlotId: slotId!,
        paymentMethod,
        substitutionPreference: substitution,
      }),
    enabled: addressId !== null && slotId !== null,
    retry: false,
    staleTime: 0,
  });
  const locationChanged =
    preview.isError && asApiError(preview.error)?.code === "CART_LOCATION_CHANGED";

  const place = useMutation({
    mutationFn: (input: { expectedTotalPaise: number; key: string }) =>
      placeOrder(
        browserHttp(),
        {
          addressId: addressId!,
          deliverySlotId: slotId!,
          paymentMethod,
          substitutionPreference: substitution,
          customerNotes: notes.trim() || null,
          expectedTotalPaise: input.expectedTotalPaise,
        },
        input.key,
      ),
  });

  async function submitOrder(expectedTotalPaise: number) {
    setPlaceError(null);
    placeKey.current ??= newIdempotencyKey();
    try {
      const order = await place.mutateAsync({ expectedTotalPaise, key: placeKey.current });
      placeKey.current = null;
      void queryClient.invalidateQueries({ queryKey: queryKeys.cart });
      router.push(`/orders/${encodeURIComponent(order.id)}/success`);
    } catch (e) {
      const apiError = asApiError(e);
      // No answer, a timeout or any 5xx may hide an order the server did place: keep the key, so the next click
      // replays that result instead of placing a second order (D315). Only a definite refusal ends the action.
      if (!apiError || !apiError.isOutcomeUnknown) placeKey.current = null;
      if (apiError?.code === "PRICE_CHANGED" && apiError.problem.preview) {
        const next = apiError.problem.preview as CheckoutPreview;
        queryClient.setQueryData(previewKey, next);
        setPriceChange({ previousTotalPaise: expectedTotalPaise, preview: next });
        return;
      }
      if (apiError?.code === "CART_LOCATION_CHANGED") {
        void preview.refetch();
        return;
      }
      setPlaceError({
        message: describeError(e),
        ...(apiError?.traceId ? { traceId: apiError.traceId } : {}),
      });
      void preview.refetch();
    }
  }

  /**
   * Saves an edited address with `If-Match` (its version). A stale version (412 `CONCURRENCY_CONFLICT`) or a lost
   * race (409 `CONCURRENT_SAVE`) reloads the addresses and asks the customer to look again; other refusals stay in
   * the form under their fields. A changed pincode can change the store and the slots, so the slot is cleared.
   */
  async function saveEditedAddress(original: Address, input: AddressInput) {
    try {
      const saved = await replaceAddress(
        browserHttp(),
        original.addressId,
        input,
        original.version,
      );
      await queryClient.invalidateQueries({ queryKey: queryKeys.addresses });
      if (saved.pincode !== original.pincode) setSlotId(null);
      setDropped(null);
      setEditing(null);
      void queryClient.invalidateQueries({ queryKey: ["checkout-preview"] });
    } catch (e) {
      if (hasCode(e, "CONCURRENCY_CONFLICT", "CONCURRENT_SAVE")) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.addresses });
        setEditing(null);
        setAddressNotice(
          "This address was changed elsewhere. We have loaded the latest version; please check it.",
        );
        return;
      }
      throw e;
    }
  }

  async function moveCartToAddress(force: boolean) {
    if (!address) return;
    try {
      await moveCart.mutateAsync({ pincode: address.pincode, force });
      choosePincode(address.pincode);
      setDropped(null);
      void preview.refetch();
    } catch (e) {
      const apiError = asApiError(e);
      if (apiError?.code === "CART_LOCATION_CHANGED") {
        setDropped(
          Array.isArray(apiError.problem.lines)
            ? (apiError.problem.lines as DroppedCartLine[])
            : [],
        );
        return;
      }
      setPlaceError({ message: describeError(e) });
    }
  }

  if (cart.isPending || addresses.isPending) {
    return (
      <div className="grid gap-6 lg:grid-cols-[1fr_24rem]" aria-busy="true">
        <span className="sr-only" role="status">
          Loading checkout
        </span>
        <div className="flex flex-col gap-4">
          <Skeleton className="h-40 rounded-[22px]" />
          <Skeleton className="h-40 rounded-[22px]" />
        </div>
        <Skeleton className="h-72 rounded-[22px]" />
      </div>
    );
  }
  if (cart.isError || addresses.isError) {
    const error = cart.error ?? addresses.error;
    return (
      <LoadError
        title="We could not load checkout"
        error={error}
        onRetry={() => {
          void cart.refetch();
          void addresses.refetch();
        }}
      />
    );
  }
  if (cart.data.lines.length === 0) {
    return (
      <EmptyPage
        slot="EMPTY_CART"
        title="Your cart is empty"
        description="Add something to your cart before checking out."
        actions={
          <Button asChild size="lg">
            <Link href="/shop">Start shopping</Link>
          </Button>
        }
      />
    );
  }

  const current = preview.data;
  const methods: { method: PaymentMethod; enabled: boolean }[] =
    current?.paymentMethods ?? DEFAULT_METHODS.map((method) => ({ method, enabled: true }));
  const chosenEnabled = methods.some((m) => m.method === paymentMethod && m.enabled);
  const canPlace =
    !!current && current.canPlaceOrder && chosenEnabled && !preview.isFetching && !place.isPending;

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[1fr_24rem]">
      <div className="flex min-w-0 flex-col gap-4">
        <StepCard
          step={1}
          title="Delivery address"
          action={
            <Button
              variant="outline"
              size="sm"
              leadingIcon={<MapPinPlus aria-hidden="true" />}
              onClick={() => setAdding(true)}
            >
              Add a new address
            </Button>
          }
        >
          {list.length > 0 ? (
            <Choices legend="Choose an address">
              {list.map((a) => (
                <OptionTile
                  key={a.addressId}
                  name={`${groupName}-address`}
                  value={a.addressId}
                  checked={a.addressId === addressId}
                  disabled={a.isServiceable === false}
                  onChange={() => {
                    setAddressId(a.addressId);
                    setSlotId(null);
                    setDropped(null);
                  }}
                  label={`${a.recipientName} · ${labelText(a.label)}`}
                  description={
                    addressText(a) +
                    (a.isServiceable === false ? " (we do not deliver here yet)" : "")
                  }
                />
              ))}
            </Choices>
          ) : (
            <p className="text-text-muted text-sm">Add the address we should deliver to.</p>
          )}
          {address && (
            <Button
              variant="ghost"
              size="sm"
              className="self-start"
              leadingIcon={<Pencil aria-hidden="true" />}
              onClick={() => {
                setAddressNotice(null);
                setEditing(address);
              }}
            >
              Edit this address
            </Button>
          )}
          {addressNotice && (
            <p role="status" className="bg-warning-subtle text-warning-text rounded-xl p-3 text-sm">
              {addressNotice}
            </p>
          )}
          <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
            {editing && (
              <DialogContent title="Edit delivery address" size="lg">
                <AddressForm
                  address={editing}
                  submitLabel="Save and use this address"
                  onCancel={() => setEditing(null)}
                  onSubmit={(input) => saveEditedAddress(editing, input)}
                />
              </DialogContent>
            )}
          </Dialog>
          <Dialog open={adding} onOpenChange={setAdding}>
            <DialogContent title="New delivery address" size="lg">
              <AddressForm
                submitLabel="Save and use this address"
                onCancel={() => setAdding(false)}
                onSubmit={async (input) => {
                  const created = await addAddress(browserHttp(), input);
                  await queryClient.invalidateQueries({ queryKey: queryKeys.addresses });
                  setAddressId(created.addressId);
                  setSlotId(null);
                  setAdding(false);
                }}
              />
            </DialogContent>
          </Dialog>
        </StepCard>

        <StepCard step={2} title="Delivery slot">
          {address ? (
            <SlotPicker pincode={address.pincode} value={slotId} onChange={setSlotId} />
          ) : (
            <p className="text-text-muted text-sm">Choose an address to see delivery slots.</p>
          )}
        </StepCard>

        <StepCard step={3} title="Pay on delivery">
          <Choices legend="How you will pay">
            {methods.map((m) => (
              <OptionTile
                key={m.method}
                name={`${groupName}-payment`}
                value={m.method}
                checked={m.method === paymentMethod}
                disabled={!m.enabled}
                onChange={() => setPaymentMethod(m.method)}
                label={paymentMethodLabels[m.method] ?? m.method}
                description={
                  m.enabled ? (PAYMENT_NOTES[m.method] ?? null) : "Not available for this order"
                }
              />
            ))}
          </Choices>
          <Choices legend="If an item is short" className="grid gap-2 sm:grid-cols-3">
            {SUBSTITUTIONS.map((s) => (
              <OptionTile
                key={s.value}
                name={`${groupName}-substitution`}
                value={s.value}
                checked={s.value === substitution}
                onChange={() => setSubstitution(s.value)}
                label={s.label}
                description={s.description}
              />
            ))}
          </Choices>
          <FormField label="Delivery notes (optional)" hint="For example, gate code or floor.">
            <Textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
              rows={3}
            />
          </FormField>
        </StepCard>
      </div>

      <div className="flex flex-col gap-4 lg:sticky lg:top-20">
        {locationChanged ? (
          <div className="bg-warning-subtle text-warning-text flex flex-col gap-3 rounded-[20px] p-4 text-sm">
            <p>
              This address is served by a different store. Your cart needs to be priced for{" "}
              {address?.pincode} before you can order.
            </p>
            {dropped && dropped.length > 0 && (
              <>
                <p className="font-semibold">These items are not sold there and will be removed:</p>
                <ul className="list-disc pl-5">
                  {dropped.map((d) => (
                    <li key={d.id}>
                      {cart.data.lines.find((l) => l.id === d.id)?.productName ?? "An item"}
                    </li>
                  ))}
                </ul>
              </>
            )}
            <Button
              size="sm"
              loading={moveCart.isPending}
              onClick={() => void moveCartToAddress(dropped !== null)}
            >
              {dropped && dropped.length > 0 ? "Remove them and continue" : "Update my cart"}
            </Button>
          </div>
        ) : current ? (
          <>
            <OrderSummary
              totals={current.totals}
              coupon={current.coupon}
              totalLabel="Estimated total"
            />
            {current.blockers.length > 0 && (
              <ul className="flex flex-col gap-1" aria-label="Before you place the order">
                {current.blockers.map((b) => (
                  <li
                    key={`${b.code}-${b.lineId ?? ""}`}
                    className="bg-warning-subtle text-warning-text rounded-xl px-3 py-2 text-sm"
                  >
                    {b.message}
                  </li>
                ))}
              </ul>
            )}
          </>
        ) : preview.isError ? (
          <LoadError
            title="We could not price your order"
            error={preview.error}
            headingLevel={3}
            onRetry={() => void preview.refetch()}
          />
        ) : preview.isFetching ? (
          <Skeleton className="h-48 rounded-[20px]" />
        ) : (
          <>
            <OrderSummary totals={cart.data.totals} coupon={cart.data.coupon} />
            <p className="text-text-muted text-xs">
              Choose an address and a slot to see the final estimate.
            </p>
          </>
        )}

        {placeError && (
          <div role="alert" className="bg-danger-subtle text-danger-text rounded-xl p-3 text-sm">
            <p>{placeError.message}</p>
            {placeError.traceId && (
              <p className="mt-1 text-xs opacity-80">Reference: {placeError.traceId}</p>
            )}
          </div>
        )}
        <p className="text-text-muted text-xs">
          You pay on delivery. Weighed items are charged on actual net weight, never above the
          maximum shown.
        </p>
      </div>

      <StickyActionBar
        className="lg:col-span-2"
        summary={current ? "Estimated total" : "Choose an address and a slot"}
        amount={current ? formatRupees(current.expectedTotalPaise) : undefined}
        action={
          <Button
            size="lg"
            disabled={!canPlace}
            loading={place.isPending}
            onClick={() => current && void submitOrder(current.expectedTotalPaise)}
          >
            {current ? `Place order · ${formatRupees(current.expectedTotalPaise)}` : "Place order"}
          </Button>
        }
      />

      <Dialog open={priceChange !== null} onOpenChange={(open) => !open && setPriceChange(null)}>
        {priceChange && (
          <DialogContent
            title="Your total has changed"
            description="Prices or availability changed since you started checking out."
            footer={
              <>
                <Button variant="outline" onClick={() => setPriceChange(null)}>
                  Review order
                </Button>
                <Button
                  loading={place.isPending}
                  disabled={!priceChange.preview.canPlaceOrder}
                  onClick={() => {
                    const next = priceChange.preview.expectedTotalPaise;
                    setPriceChange(null);
                    // A new decision on a new total: a new Idempotency-Key.
                    placeKey.current = null;
                    void submitOrder(next);
                  }}
                >
                  Place order at {formatRupees(priceChange.preview.expectedTotalPaise)}
                </Button>
              </>
            }
          >
            <dl className="flex flex-col gap-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-text-muted">Previous estimate</dt>
                <dd className="text-text-muted line-through">
                  {formatRupees(priceChange.previousTotalPaise)}
                </dd>
              </div>
              <div className="text-text flex justify-between text-base font-bold">
                <dt>New estimate</dt>
                <dd>{formatRupees(priceChange.preview.expectedTotalPaise)}</dd>
              </div>
            </dl>
            {priceChange.preview.blockers.length > 0 && (
              <ul className="flex flex-col gap-1">
                {priceChange.preview.blockers.map((b) => (
                  <li
                    key={b.code}
                    className="bg-warning-subtle text-warning-text rounded-md px-3 py-2 text-sm"
                  >
                    {b.message}
                  </li>
                ))}
              </ul>
            )}
          </DialogContent>
        )}
      </Dialog>
    </div>
  );
}
