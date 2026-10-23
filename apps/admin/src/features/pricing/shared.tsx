import { formatDateTime, isApiError } from "@fresh/core";
import { Checkbox, FormField, Input } from "@fresh/ui";
import { type ReactNode } from "react";
import {
  Controller,
  type FieldPath,
  type FieldValues,
  type UseFormReturn,
  get,
} from "react-hook-form";
import { type ChargeBasis } from "../../api/types/pricing";

export const PRICE_UNIT: Record<string, string> = {
  KG: "per kg",
  PIECE: "per piece",
  PACK: "per pack",
};

export const CHARGE_BASIS: Record<ChargeBasis, string> = {
  PER_KG: "Per kg",
  PER_ITEM: "Per item",
  FLAT: "Flat per line",
};

/** "12 Oct 2026, 6:00 am → open" for a validity period. */
export function periodText(validFrom: string, validTo: string | null): string {
  return `${formatDateTime(validFrom)} → ${validTo ? formatDateTime(validTo) : "open"}`;
}

/** True when the refusal (top-level or on any field) carries this code. */
export function hasCode(error: unknown, code: string): boolean {
  if (!isApiError(error)) return false;
  if (error.code === code) return true;
  return Object.values(error.errors).some((list) => list.some((e) => e.code === code));
}

function errorOf<T extends FieldValues>(
  form: UseFormReturn<T>,
  name: FieldPath<T>,
): string | undefined {
  return (get(form.formState.errors, name) as { message?: string } | undefined)?.message;
}

/** `datetime-local` in India time; empty means "now" on the server. */
export function StartField<T extends FieldValues>({
  form,
  name,
  label = "Starts (India time)",
}: {
  form: UseFormReturn<T>;
  name: FieldPath<T>;
  label?: string;
}) {
  return (
    <FormField label={label} hint="Leave empty to start now." error={errorOf(form, name)}>
      <Input type="datetime-local" {...form.register(name)} />
    </FormField>
  );
}

export function CheckboxField<T extends FieldValues>({
  form,
  name,
  label,
  description,
}: {
  form: UseFormReturn<T>;
  name: FieldPath<T>;
  label: string;
  description?: ReactNode;
}) {
  const error = errorOf(form, name);
  return (
    <div className="flex flex-col gap-1">
      <Controller
        control={form.control}
        name={name}
        render={({ field }) => (
          <Checkbox
            label={label}
            {...(typeof description === "string" ? { description } : {})}
            checked={Boolean(field.value)}
            onCheckedChange={(v) => field.onChange(v === true)}
          />
        )}
      />
      {error && (
        <p role="alert" className="text-danger-text text-xs font-medium">
          {error}
        </p>
      )}
    </div>
  );
}
