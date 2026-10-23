import { isApiError } from "@fresh/core";
import {
  Button,
  Dialog,
  DialogContent,
  FormField,
  Input,
  Select,
  type SelectOption,
  Textarea,
} from "@fresh/ui";
import { AlertCircle } from "lucide-react";
import { type ReactNode, useId, useState } from "react";
import { Link } from "react-router";
import { type ListParams } from "../../lib/list-params";
import { errorMessage } from "../../lib/errors";
import { reportError } from "../../lib/feedback";

// Labels and small building blocks shared by the payments screens. Codes come from the API; labels are UI copy.

export const METHOD_LABELS: Readonly<Record<string, string>> = {
  COD: "Cash on delivery",
  UPI_ON_DELIVERY: "UPI on delivery",
  ONLINE: "Online",
};

export const REFUND_REASON_LABELS: Readonly<Record<string, string>> = {
  COMPLAINT: "Complaint",
  GOODWILL: "Goodwill",
  WEIGHT_SHORTFALL: "Weight shortfall",
  SHORT_SUPPLY: "Short supply",
  CANCELLATION: "Cancellation",
  RETURN: "Return",
};

export const REFUND_METHOD_LABELS: Readonly<Record<string, string>> = {
  ORIGINAL: "Original payment",
  UPI: "UPI",
  BANK_TRANSFER: "Bank transfer",
  CASH: "Cash",
};

export const ISSUE_KIND_LABELS: Readonly<Record<string, string>> = {
  AMOUNT_MISMATCH: "Amount mismatch",
  MISSED_WEBHOOK: "Missed webhook",
  COLLECTION_DIFFERENCE: "Collection difference",
  REFUND_CAPPED: "Refund capped",
  REFUND_STUCK: "Refund stuck",
  DELIVERED_NOT_COLLECTED: "Delivered, not collected",
  UNSETTLED_CASH: "Unsettled cash",
  SETTLEMENT_AGED: "Settlement aged",
  CASH_DIFFERENCE: "Cash difference",
};

/** Plain-language messages for payment refusals (branch on code, never on text). */
export const PAYMENT_CODE_MESSAGES: Readonly<Record<string, string>> = {
  REFUND_SELF_APPROVAL: "You requested this refund, so another staff member must approve it.",
  REFUND_NOT_APPROVABLE:
    "This refund is no longer waiting for approval. Reload to see its current state.",
  REFUND_EXCEEDS_PAYMENT: "The amount is more than what can still be refunded on this order.",
  NO_CAPTURED_PAYMENT:
    "Nothing has been paid or collected on this order yet, so there is nothing to refund.",
  SETTLEMENT_EXISTS: "A settlement for this rider and date already exists.",
  SETTLEMENT_SELF_RESOLUTION:
    "You recorded this settlement with a difference, so another staff member must settle it.",
  LOCATION_ACCESS_DENIED: "You do not have access to this location.",
};

export function codeMessage(error: unknown): string {
  if (isApiError(error) && PAYMENT_CODE_MESSAGES[error.code])
    return PAYMENT_CODE_MESSAGES[error.code]!;
  return errorMessage(error);
}

export function options(labels: Readonly<Record<string, string>>): SelectOption[] {
  return Object.entries(labels).map(([value, label]) => ({ value, label }));
}

const ANY = "__any";

/** A single-value filter bound to a URL key; "Any" removes it. */
export function FilterSelect({
  list,
  name,
  label,
  choices,
}: {
  list: ListParams;
  name: string;
  label: string;
  choices: readonly SelectOption[];
}) {
  return (
    <div className="w-48">
      <Select
        aria-label={label}
        options={[{ value: ANY, label: `${label}: any` }, ...choices]}
        value={list.get(name) ?? ANY}
        onValueChange={(v) => list.set({ [name]: v === ANY ? null : v })}
      />
    </div>
  );
}

export function DateFilter({
  list,
  name,
  label,
}: {
  list: ListParams;
  name: string;
  label: string;
}) {
  return (
    <div className="w-40">
      <Input
        type="date"
        aria-label={label}
        title={label}
        value={list.get(name) ?? ""}
        onChange={(e) => list.set({ [name]: e.target.value || null })}
      />
    </div>
  );
}

/** Chips for the active filters, labelled from the same maps as the controls. */
export function activeFilters(
  list: ListParams,
  defs: readonly { name: string; label: string; labels?: Readonly<Record<string, string>> }[],
) {
  return defs.flatMap((d) => {
    const value = list.get(d.name);
    if (!value) return [];
    return [
      {
        id: d.name,
        label: `${d.label}: ${d.labels?.[value] ?? value}`,
        onRemove: () => list.set({ [d.name]: null }),
      },
    ];
  });
}

export function Banner({ children }: { children: ReactNode }) {
  return (
    <p
      role="alert"
      className="bg-danger-subtle text-danger-text flex items-start gap-2 rounded-md px-3 py-2 text-sm"
    >
      <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      {children}
    </p>
  );
}

/**
 * Dialog with one text field (reason, note, reference) for a command that quotes `If-Match`. Field errors for
 * `field` show at the input; known payment codes show as a banner; 412 opens the reload prompt.
 */
export function TextCommandDialog({
  open,
  onOpenChange,
  title,
  description,
  field,
  label,
  hint,
  required,
  minLength,
  maxLength,
  multiline = true,
  submitLabel,
  danger,
  onSubmit,
  onConflict,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  /** API field name, e.g. `reason`, `note`, `reference`. */
  field: string;
  label: string;
  hint?: string;
  required: boolean;
  minLength?: number;
  maxLength: number;
  multiline?: boolean;
  submitLabel: string;
  danger?: boolean;
  onSubmit: (value: string) => Promise<unknown>;
  onConflict: () => unknown;
}) {
  const [value, setValue] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const formId = useId();

  const close = (next: boolean) => {
    if (!next) {
      setValue("");
      setFieldError(null);
      setBanner(null);
    }
    onOpenChange(next);
  };

  const submit = async () => {
    const trimmed = value.trim();
    if (required && trimmed.length === 0) return setFieldError(`Enter the ${label.toLowerCase()}.`);
    if (minLength && trimmed.length > 0 && trimmed.length < minLength)
      return setFieldError(`${label} must be at least ${minLength} characters.`);
    if (trimmed.length > maxLength)
      return setFieldError(`${label} must be at most ${maxLength} characters.`);
    setFieldError(null);
    setBanner(null);
    setPending(true);
    try {
      await onSubmit(trimmed);
      close(false);
    } catch (error) {
      const message = isApiError(error) ? error.fieldMessage(field) : undefined;
      if (message) setFieldError(message);
      else if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT") {
        close(false);
        reportError(error, { onConflict });
      } else if (
        isApiError(error) &&
        (PAYMENT_CODE_MESSAGES[error.code] || error.status === 422 || error.status === 409)
      ) {
        setBanner(codeMessage(error));
      } else reportError(error, { onConflict });
    } finally {
      setPending(false);
    }
  };

  const control = multiline ? (
    <Textarea
      rows={3}
      value={value}
      onChange={(e) => setValue(e.target.value)}
      maxLength={maxLength}
    />
  ) : (
    <Input value={value} onChange={(e) => setValue(e.target.value)} maxLength={maxLength} />
  );

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent
        size="sm"
        title={title}
        {...(description ? { description } : {})}
        footer={
          <>
            <Button variant="outline" onClick={() => close(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              form={formId}
              variant={danger ? "danger" : "primary"}
              loading={pending}
            >
              {submitLabel}
            </Button>
          </>
        }
      >
        <form
          id={formId}
          noValidate
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          {banner && <Banner>{banner}</Banner>}
          <FormField
            label={label}
            required={required}
            error={fieldError}
            {...(hint ? { hint } : {})}
          >
            {control}
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** Sub-navigation between the payments screens. */
export function PaymentsTabs({
  current,
  showWebhooks,
}: {
  current: "payments" | "webhooks";
  showWebhooks: boolean;
}) {
  if (!showWebhooks) return null;
  const item = (id: "payments" | "webhooks", href: string, label: string) => (
    <Link
      to={href}
      aria-current={current === id ? "page" : undefined}
      className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${
        current === id
          ? "border-accent text-text"
          : "text-text-muted hover:text-text border-transparent"
      }`}
    >
      {label}
    </Link>
  );
  return (
    <nav aria-label="Payments sections" className="border-border mb-4 flex gap-1 border-b">
      {item("payments", "/payments", "Payments")}
      {item("webhooks", "/payments/webhook-events", "Webhook events")}
    </nav>
  );
}
