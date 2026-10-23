import { AlertCircle } from "lucide-react";

/** A refusal that names no form field (LAST_ADMIN, ROLE_IN_USE …), shown above the form. */
export function FormAlert({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <div
      role="alert"
      className="border-danger bg-danger-subtle text-danger-text flex items-start gap-2 rounded-md border p-3 text-sm"
    >
      <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      <span>{message}</span>
    </div>
  );
}
