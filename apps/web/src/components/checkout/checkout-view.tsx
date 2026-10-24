"use client";

import { formatRupees, newIdempotencyKey } from "@fresh/core";
import {
  Button,
  Card,
  CardContent,
  Dialog,
  DialogContent,
  EmptyState,
  ErrorState,
  FormField,
  RadioGroup,
  Skeleton,
  Textarea,
} from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPinPlus, ShoppingBag } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type ReactNode, useRef, useState } from "react";
import { browserHttp } from "@/api/browser";
import { addAddress, listAddresses } from "@/api/endpoints/account";
import { previewCheckout, placeOrder } from "@/api/endpoints/orders";
import { type DroppedCartLine } from "@/api/types/cart";
import {
  type Address,
  type CheckoutPreview,
  type PaymentMethod,
  type SubstitutionPreference,
} from "@/api/types/checkout";
import { AddressForm } from "@/components/account/address-form";
import { OrderSummary } from "@/components/cart/order-summary";
import { useCart, useChangeCartLocation } from "@/cart/use-cart";
import { asApiError, describeError } from "@/lib/errors";
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

function Step({ n, title, children }: { n: number; title: string; children: ReactNode }) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <h2 className="text-text flex items-center gap-3 text-lg font-semibold">
          <span
            aria-hidden="true"
            className="bg-primary text-on-primary flex size-7 items-center justify-center rounded-full text-sm"
          >
            {n}
          </span>
          {title}
        </h2>
        {children}
      </CardContent>
    </Card>
  );
}

function addressText(a: Address): string {
  return [a.line1, a.line2, a.landmark, a.locality, `${a.cityName} ${a.pincode}`]
    .filter(Boolean)
    .join(", ");
}

type PriceChange = { previousTotalPaise: number; preview: CheckoutPreview };

export function CheckoutView() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { choosePincode } = useLocation();
  const cart = useCart();
  const moveCart = useChangeCartLocation();
  const addresses = useQuery({
    queryKey: queryKeys.addresses,
    queryFn: () => listAddresses(browserHttp()),
  });

  const [chosenAddressId, setAddressId] = useState<string | null>(null);
  const [slotId, setSlotId] = useState<string | null>(null);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("COD");
  const [substitution, setSubstitution] = useState<SubstitutionPreference>("CALL_ME");
  const [notes, setNotes] = useState("");
  const [adding, setAdding] = useState(false);
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
      // A retryable failure keeps the key so a second click cannot place a second order.
      if (!apiError?.isRetryable) placeKey.current = null;
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
      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]" aria-busy="true">
        <span className="sr-only" role="status">
          Loading checkout
        </span>
        <div className="flex flex-col gap-4">
          <Skeleton className="h-40 rounded-xl" />
          <Skeleton className="h-40 rounded-xl" />
        </div>
        <Skeleton className="h-72 rounded-xl" />
      </div>
    );
  }
  if (cart.isError || addresses.isError) {
    const error = cart.error ?? addresses.error;
    return (
      <ErrorState
        title="We could not load checkout"
        description={describeError(error)}
        traceId={asApiError(error)?.traceId}
        onRetry={() => {
          void cart.refetch();
          void addresses.refetch();
        }}
      />
    );
  }
  if (cart.data.lines.length === 0) {
    return (
      <EmptyState
        icon={<ShoppingBag aria-hidden="true" />}
        title="Your cart is empty"
        description="Add something to your cart before checking out."
        action={
          <Button asChild>
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
    <div className="grid items-start gap-6 lg:grid-cols-[1fr_22rem]">
      <div className="flex flex-col gap-4">
        <Step n={1} title="Delivery address">
          {list.length > 0 && (
            <RadioGroup
              label="Choose an address"
              value={addressId ?? undefined}
              onValueChange={(id) => {
                setAddressId(id);
                setSlotId(null);
                setDropped(null);
              }}
              options={list.map((a) => ({
                value: a.addressId,
                label: `${a.recipientName} · ${a.label.charAt(0)}${a.label.slice(1).toLowerCase()}`,
                description:
                  addressText(a) +
                  (a.isServiceable === false ? " (we do not deliver here yet)" : ""),
                ...(a.isServiceable === false ? { disabled: true } : {}),
              }))}
            />
          )}
          {list.length === 0 && (
            <p className="text-text-muted text-sm">Add the address we should deliver to.</p>
          )}
          <Dialog open={adding} onOpenChange={setAdding}>
            <Button
              variant="outline"
              className="self-start"
              leadingIcon={<MapPinPlus aria-hidden="true" />}
              onClick={() => setAdding(true)}
            >
              Add a new address
            </Button>
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
        </Step>

        <Step n={2} title="Delivery slot">
          {address ? (
            <SlotPicker pincode={address.pincode} value={slotId} onChange={setSlotId} />
          ) : (
            <p className="text-text-muted text-sm">Choose an address to see delivery slots.</p>
          )}
        </Step>

        <Step n={3} title="Payment and preferences">
          <RadioGroup
            label="Pay on delivery"
            value={paymentMethod}
            onValueChange={(v) => setPaymentMethod(v as PaymentMethod)}
            options={methods.map((m) => ({
              value: m.method,
              label: paymentMethodLabels[m.method] ?? m.method,
              ...(m.enabled ? {} : { disabled: true, description: "Not available for this order" }),
            }))}
          />
          <RadioGroup
            label="If an item is short"
            value={substitution}
            onValueChange={(v) => setSubstitution(v as SubstitutionPreference)}
            options={SUBSTITUTIONS}
          />
          <FormField label="Delivery notes (optional)" hint="For example, gate code or floor.">
            <Textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
              rows={3}
            />
          </FormField>
        </Step>
      </div>

      <Card className="lg:sticky lg:top-24">
        <CardContent className="flex flex-col gap-5">
          <h2 className="text-text text-lg font-semibold">Order summary</h2>
          {locationChanged ? (
            <div className="bg-warning-subtle text-warning-text flex flex-col gap-3 rounded-lg p-3 text-sm">
              <p>
                This address is served by a different store. Your cart needs to be priced for{" "}
                {address?.pincode} before you can order.
              </p>
              {dropped && dropped.length > 0 && (
                <>
                  <p className="font-semibold">
                    These items are not sold there and will be removed:
                  </p>
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
                      className="bg-warning-subtle text-warning-text rounded-md px-3 py-2 text-sm"
                    >
                      {b.message}
                    </li>
                  ))}
                </ul>
              )}
            </>
          ) : preview.isError ? (
            <ErrorState
              title="We could not price your order"
              description={describeError(preview.error)}
              traceId={asApiError(preview.error)?.traceId}
              onRetry={() => void preview.refetch()}
            />
          ) : preview.isFetching ? (
            <Skeleton className="h-40" />
          ) : (
            <>
              <OrderSummary totals={cart.data.totals} coupon={cart.data.coupon} />
              <p className="text-text-muted text-xs">
                Choose an address and a slot to see the final estimate.
              </p>
            </>
          )}

          {placeError && (
            <div role="alert" className="bg-danger-subtle text-danger-text rounded-lg p-3 text-sm">
              <p>{placeError.message}</p>
              {placeError.traceId && (
                <p className="mt-1 text-xs opacity-80">Reference: {placeError.traceId}</p>
              )}
            </div>
          )}

          <Button
            size="lg"
            className="w-full"
            disabled={!canPlace}
            loading={place.isPending}
            onClick={() => current && void submitOrder(current.expectedTotalPaise)}
          >
            {current ? `Place order · ${formatRupees(current.expectedTotalPaise)}` : "Place order"}
          </Button>
          <p className="text-text-muted text-xs">
            You pay on delivery. Weighed items are charged on actual net weight, never above the
            maximum shown.
          </p>
        </CardContent>
      </Card>

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
