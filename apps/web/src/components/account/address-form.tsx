"use client";

import { Button, Checkbox, FormField, Input, RadioGroup } from "@fresh/ui";
import { type FormEvent, useState } from "react";
import { browserHttp } from "@/api/browser";
import { getServiceability } from "@/api/endpoints/catalog";
import { type Address, type AddressInput, type AddressLabel } from "@/api/types/checkout";
import { isValidPincode } from "@/lib/cookies";
import { asApiError, describeError } from "@/lib/errors";

type FieldName = Exclude<keyof AddressInput, "isDefault">;

const LABELS: { value: AddressLabel; label: string }[] = [
  { value: "HOME", label: "Home" },
  { value: "WORK", label: "Work" },
  { value: "OTHER", label: "Other" },
];

function initialInput(address: Address | undefined): AddressInput {
  return {
    label: address?.label ?? "HOME",
    recipientName: address?.recipientName ?? "",
    phone: address?.phone.replace(/^\+91/, "") ?? "",
    line1: address?.line1 ?? "",
    line2: address?.line2 ?? null,
    landmark: address?.landmark ?? null,
    locality: address?.locality ?? "",
    cityName: address?.cityName ?? "",
    stateCode: address?.stateCode ?? "",
    pincode: address?.pincode ?? "",
    isDefault: address?.isDefault ?? false,
  };
}

/** Basic shape checks so obvious typos are caught before a round trip; the server's rules are authoritative. */
function localErrors(input: AddressInput): Partial<Record<FieldName, string>> {
  const errors: Partial<Record<FieldName, string>> = {};
  if (!input.recipientName.trim())
    errors.recipientName = "Enter the name of the person receiving the order.";
  if (!/^[6-9][0-9]{9}$/.test(input.phone)) errors.phone = "Enter a 10-digit mobile number.";
  if (!input.line1.trim()) errors.line1 = "Enter the house or flat number and street.";
  if (!input.locality.trim()) errors.locality = "Enter the locality or area.";
  if (!input.cityName.trim()) errors.cityName = "Enter the city.";
  if (!/^[A-Za-z]{2}$/.test(input.stateCode)) errors.stateCode = "Enter the two-letter state code.";
  if (!isValidPincode(input.pincode)) errors.pincode = "Enter the 6-digit pincode.";
  return errors;
}

export interface AddressFormProps {
  address?: Address;
  submitLabel?: string;
  /** Receives the input with the phone in E.164; throw the ApiError to show the server's field messages. */
  onSubmit: (input: AddressInput) => Promise<void>;
  onCancel?: () => void;
}

export function AddressForm({
  address,
  submitLabel = "Save address",
  onSubmit,
  onCancel,
}: AddressFormProps) {
  const [input, setInput] = useState<AddressInput>(() => initialInput(address));
  const [errors, setErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  function set<K extends keyof AddressInput>(key: K, value: AddressInput[K]) {
    setInput((current) => ({ ...current, [key]: value }));
  }

  /**
   * A full pincode fills an empty city and state from the server: the city from `GET /serviceability`, its state
   * code from `GET /cities`. Fields the customer already typed are left alone; on any failure they simply type them.
   */
  async function prefillFromPincode(pincode: string) {
    try {
      // The server names the city and its state with the pincode (LOC-R19).
      const area = await getServiceability(browserHttp(), pincode);
      const city = area.city;
      if (!city) return;
      setInput((current) =>
        current.pincode !== pincode
          ? current
          : {
              ...current,
              cityName: current.cityName.trim() ? current.cityName : city.name,
              stateCode: current.stateCode.trim() ? current.stateCode : city.stateCode,
            },
      );
    } catch {
      // Not resolved: the customer types the city and state.
    }
  }

  function changePincode(value: string) {
    const pincode = value.replace(/\D/g, "").slice(0, 6);
    set("pincode", pincode);
    if (isValidPincode(pincode)) void prefillFromPincode(pincode);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const local = localErrors(input);
    setErrors(local);
    setFormError(null);
    if (Object.keys(local).length > 0) return;
    setPending(true);
    try {
      await onSubmit({
        ...input,
        phone: `+91${input.phone}`,
        stateCode: input.stateCode.toUpperCase(),
        line2: input.line2?.trim() || null,
        landmark: input.landmark?.trim() || null,
      });
    } catch (e) {
      const apiError = asApiError(e);
      const fieldErrors: Partial<Record<FieldName, string>> = {};
      for (const [field, list] of Object.entries(apiError?.errors ?? {})) {
        const message = list[0]?.message;
        if (message && field in input) fieldErrors[field as FieldName] = message;
      }
      setErrors(fieldErrors);
      if (Object.keys(fieldErrors).length === 0) setFormError(describeError(e));
    } finally {
      setPending(false);
    }
  }

  const text = (name: FieldName, label: string, props: Record<string, unknown> = {}) => (
    <FormField label={label} error={errors[name]} required={!["line2", "landmark"].includes(name)}>
      <Input
        value={(input[name] as string | null) ?? ""}
        onChange={(e) => set(name, e.target.value)}
        {...props}
      />
    </FormField>
  );

  return (
    <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-4">
      <RadioGroup
        label="Save as"
        orientation="horizontal"
        value={input.label}
        onValueChange={(v) => set("label", v as AddressLabel)}
        options={LABELS}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        {text("recipientName", "Recipient name", { autoComplete: "name" })}
        <FormField label="Mobile number" error={errors.phone} required>
          <Input
            type="tel"
            inputMode="numeric"
            autoComplete="tel-national"
            maxLength={10}
            value={input.phone}
            onChange={(e) => set("phone", e.target.value.replace(/\D/g, ""))}
            startAdornment={<span className="text-text-muted text-sm">+91</span>}
          />
        </FormField>
      </div>
      {text("line1", "House, flat and street", { autoComplete: "address-line1" })}
      {text("line2", "Apartment or building (optional)", { autoComplete: "address-line2" })}
      <div className="grid gap-4 sm:grid-cols-2">
        {text("landmark", "Landmark (optional)")}
        {text("locality", "Locality or area", { autoComplete: "address-level3" })}
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField label="Pincode" error={errors.pincode} required>
          <Input
            inputMode="numeric"
            maxLength={6}
            autoComplete="postal-code"
            value={input.pincode}
            onChange={(e) => changePincode(e.target.value)}
          />
        </FormField>
        {text("cityName", "City", { autoComplete: "address-level2" })}
        {text("stateCode", "State code", { maxLength: 2, placeholder: "e.g. KA" })}
      </div>
      <Checkbox
        label="Use as my default address"
        checked={input.isDefault ?? false}
        onCheckedChange={(checked) => set("isDefault", checked === true)}
      />
      {formError && (
        <p role="alert" className="text-danger-text text-sm">
          {formError}
        </p>
      )}
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" loading={pending}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
