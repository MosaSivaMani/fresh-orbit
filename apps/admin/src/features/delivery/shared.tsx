import { isApiError } from "@fresh/core";
import { AlertCircle } from "lucide-react";
import { type ReactNode } from "react";
import { type FieldValues, type Path, type UseFormSetError } from "react-hook-form";
import { NavLink } from "react-router";
import { errorMessage } from "../../lib/errors";
import { reportError } from "../../lib/feedback";

/** Inline refusal shown inside a dialog or form (one sentence from the server). */
export function FormAlert({ children }: { children: ReactNode }) {
  if (!children) return null;
  return (
    <div
      role="alert"
      className="bg-danger-subtle text-danger-text flex items-start gap-2 rounded-md px-3 py-2 text-sm"
    >
      <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      <div>{children}</div>
    </div>
  );
}

/**
 * Report a failed write. Refusal codes that belong to one field (e.g. DELIVERY_CODE_INVALID → deliveryCode)
 * go to that field even when the server sends no `errors` member; everything else follows reportError.
 */
export function reportWriteError<T extends FieldValues>(
  error: unknown,
  options: {
    setError: UseFormSetError<T>;
    fields: readonly string[];
    codeFields?: Readonly<Record<string, string>>;
    aliases?: Readonly<Record<string, string>>;
    onConflict?: () => unknown;
    title?: string;
  },
): void {
  if (isApiError(error) && Object.keys(error.errors).length === 0) {
    const field = options.codeFields?.[error.code];
    if (field) {
      options.setError(
        field as Path<T>,
        { type: error.code, message: errorMessage(error) },
        { shouldFocus: true },
      );
      return;
    }
  }
  reportError(error, {
    setError: options.setError,
    fields: options.fields,
    ...(options.aliases ? { aliases: options.aliases } : {}),
    ...(options.onConflict ? { onConflict: options.onConflict } : {}),
    ...(options.title ? { title: options.title } : {}),
  });
}

const DELIVERY_TABS = [
  { to: "/delivery", label: "Shipments", end: true },
  { to: "/delivery/slots", label: "Slots", end: false },
  { to: "/delivery/templates", label: "Slot templates", end: false },
  { to: "/delivery/riders", label: "Riders", end: false },
] as const;

export function DeliveryNav() {
  return (
    <nav aria-label="Delivery sections" className="border-border mb-6 flex gap-1 border-b">
      {DELIVERY_TABS.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to}
          end={tab.end}
          className={({ isActive }) =>
            `-mb-px inline-flex h-10 items-center border-b-2 px-3 text-sm font-medium ${
              isActive
                ? "border-accent text-text"
                : "text-text-muted hover:text-text border-transparent"
            }`
          }
        >
          {tab.label}
        </NavLink>
      ))}
    </nav>
  );
}

export const DAYS = [
  { value: "1", label: "Monday" },
  { value: "2", label: "Tuesday" },
  { value: "3", label: "Wednesday" },
  { value: "4", label: "Thursday" },
  { value: "5", label: "Friday" },
  { value: "6", label: "Saturday" },
  { value: "7", label: "Sunday" },
] as const;

export function dayLabel(day: number): string {
  return DAYS.find((d) => d.value === String(day))?.label ?? String(day);
}

/** Short, recognisable form of a UUID for staff (last 6 characters). */
export function shortId(id: string): string {
  return id.slice(-6).toUpperCase();
}
