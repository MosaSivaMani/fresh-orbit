"use client";

import { formatRupees, formatTime, formatDayShort } from "@fresh/core";
import { Button, FormField, Input } from "@fresh/ui";
import { MapPin } from "lucide-react";
import { type FormEvent, useId, useState } from "react";
import { browserHttp } from "@/api/browser";
import { getServiceability } from "@/api/endpoints/catalog";
import { type DroppedCartLine } from "@/api/types/cart";
import { type ServiceabilityResponse } from "@/api/types/locations";
import { useCart, useChangeCartLocation } from "@/cart/use-cart";
import { isValidPincode } from "@/lib/cookies";
import { cx } from "@/lib/cx";
import { asApiError, describeError } from "@/lib/errors";
import { useLocation } from "./location-context";

type Step =
  | { kind: "idle" }
  | { kind: "checking" }
  | { kind: "unserviceable"; pincode: string }
  | { kind: "confirmDrop"; result: ServiceabilityResponse; dropped: DroppedCartLine[] }
  | { kind: "done"; result: ServiceabilityResponse };

export interface PincodeFormProps {
  /** Called after a serviceable pincode has been stored. */
  onDone?: () => void;
  /** `hero`: the white box in the home hero (label hidden, answer in a white card). Default: the dialog form. */
  variant?: "default" | "hero";
}

/**
 * Pincode entry with the server's serviceability answer. A pincode is stored only when the server says it is served;
 * if the cart is priced for another location, the cart moves first (CART-R25) and dropped lines need confirmation.
 */
export function PincodeForm({ onDone, variant = "default" }: PincodeFormProps) {
  const hero = variant === "hero";
  const { pincode: current, choosePincode } = useLocation();
  const cart = useCart();
  const moveCart = useChangeCartLocation();
  const [value, setValue] = useState(current ?? "");
  const [inputError, setInputError] = useState<string | undefined>();
  const [step, setStep] = useState<Step>({ kind: "idle" });
  const [error, setError] = useState<string | null>(null);
  const resultId = useId();

  const cartPincode = cart.data?.pincode ?? null;
  const cartHasLines = (cart.data?.lines.length ?? 0) > 0;

  function finish(result: ServiceabilityResponse) {
    choosePincode(result.pincode);
    setStep({ kind: "done", result });
    onDone?.();
  }

  async function moveCartThenFinish(result: ServiceabilityResponse, force: boolean) {
    try {
      await moveCart.mutateAsync({ pincode: result.pincode, force });
      finish(result);
    } catch (e) {
      const apiError = asApiError(e);
      if (apiError?.code === "CART_LOCATION_CHANGED") {
        const dropped = Array.isArray(apiError.problem.lines)
          ? (apiError.problem.lines as DroppedCartLine[])
          : [];
        setStep({ kind: "confirmDrop", result, dropped });
        return;
      }
      setStep({ kind: "idle" });
      setError(describeError(e));
    }
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const pincode = value.trim();
    if (!isValidPincode(pincode)) {
      setInputError("Enter your 6-digit delivery pincode.");
      return;
    }
    setInputError(undefined);
    setStep({ kind: "checking" });
    try {
      const result = await getServiceability(browserHttp(), pincode);
      if (!result.serviceable) {
        setStep({ kind: "unserviceable", pincode });
        return;
      }
      if (cartHasLines && cartPincode !== null && cartPincode !== pincode) {
        await moveCartThenFinish(result, false);
        return;
      }
      finish(result);
    } catch (e) {
      setStep({ kind: "idle" });
      setError(describeError(e));
    }
  }

  const lineName = (id: string) =>
    cart.data?.lines.find((line) => line.id === id)?.productName ?? "An item";

  return (
    <div className={cx("flex flex-col gap-4", hero && "gap-2 text-left")}>
      <form
        onSubmit={onSubmit}
        noValidate
        className={cx(
          "flex flex-col gap-3 sm:flex-row sm:items-end",
          hero &&
            "bg-surface flex-row items-start gap-2 rounded-2xl p-1.5 shadow-[0_6px_16px_rgb(0_0_0/0.12)] sm:items-start",
        )}
      >
        <FormField
          label="Delivery pincode"
          hideLabel={hero}
          hint={hero ? undefined : "We show prices and stock for the store that delivers to you."}
          error={inputError}
          className="flex-1"
        >
          <Input
            value={value}
            onChange={(e) => setValue(e.target.value.replace(/\D/g, "").slice(0, 6))}
            inputMode="numeric"
            autoComplete="postal-code"
            maxLength={6}
            placeholder={hero ? "Enter pincode, e.g. 533001" : "e.g. 533001"}
            className={hero ? "h-11 border-0 shadow-none" : undefined}
            startAdornment={<MapPin aria-hidden="true" className="size-4" />}
            aria-describedby={step.kind !== "idle" ? resultId : undefined}
          />
        </FormField>
        <Button
          type="submit"
          size={hero ? "md" : "lg"}
          className={hero ? "h-11 rounded-xl" : undefined}
          loading={step.kind === "checking"}
        >
          Check
        </Button>
      </form>

      <div
        id={resultId}
        aria-live="polite"
        className={cx("flex flex-col gap-3", hero && "[&>*]:shadow-[0_6px_16px_rgb(0_0_0/0.12)]")}
      >
        {error && (
          <p
            role="alert"
            className={cx("text-danger-text text-sm", hero && "bg-surface rounded-lg px-3 py-2")}
          >
            {error}
          </p>
        )}
        {step.kind === "unserviceable" && (
          <div className="bg-warning-subtle text-warning-text rounded-lg p-4 text-sm">
            <p className="font-semibold">We do not deliver to {step.pincode} yet.</p>
            <p>You can still browse the shop; prices appear once you choose a pincode we serve.</p>
          </div>
        )}
        {step.kind === "confirmDrop" && (
          <div className="bg-warning-subtle text-warning-text flex flex-col gap-3 rounded-lg p-4 text-sm">
            <p className="font-semibold">
              Some items in your cart are not sold at {step.result.pincode}:
            </p>
            <ul className="list-disc pl-5">
              {step.dropped.map((line) => (
                <li key={line.id}>{lineName(line.id)}</li>
              ))}
            </ul>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="primary"
                loading={moveCart.isPending}
                onClick={() => void moveCartThenFinish(step.result, true)}
              >
                Change pincode and remove them
              </Button>
              <Button variant="outline" onClick={() => setStep({ kind: "idle" })}>
                Keep {cartPincode ?? "current pincode"}
              </Button>
            </div>
          </div>
        )}
        {step.kind === "done" && step.result.serviceArea && (
          <div className="bg-success-subtle text-success-text rounded-lg p-4 text-sm">
            <p className="font-semibold">
              We deliver to {step.result.pincode}
              {step.result.city ? `, ${step.result.city.name}` : ""}.
            </p>
            <p>
              Delivery fee {formatRupees(step.result.serviceArea.deliveryFeePaise)}
              {step.result.serviceArea.freeDeliveryAbovePaise != null &&
                ` (free above ${formatRupees(step.result.serviceArea.freeDeliveryAbovePaise)})`}
              . Minimum order {formatRupees(step.result.serviceArea.minOrderPaise)}.
            </p>
            {step.result.nextSlot && (
              <p>
                Next delivery slot: {formatDayShort(step.result.nextSlot.startAt)},{" "}
                {formatTime(step.result.nextSlot.startAt)} –{" "}
                {formatTime(step.result.nextSlot.endAt)}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
