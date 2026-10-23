import { formatDateTime, formatRupees, isApiError } from "@fresh/core";
import {
  Button,
  Dialog,
  DialogContent,
  ErrorState,
  FormField,
  Input,
  Select,
  Skeleton,
  StatusBadge,
  Textarea,
} from "@fresh/ui";
import { useId, useState } from "react";
import { Link } from "react-router";
import { useCreateRefund, useOrderPayments } from "../../api/payments";
import { type Id } from "../../api/types/common";
import {
  REFUND_METHODS,
  REFUND_REASONS,
  type RefundMethod,
  type RefundReason,
} from "../../api/types/payments";
import { useCan } from "../../app/services";
import { Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import { PAYMENT_STATUS } from "../../lib/status";
import { parseRupeesToPaise } from "../../lib/units";
import { RefundsTable } from "./RefundsTable";
import {
  Banner,
  METHOD_LABELS,
  PAYMENT_CODE_MESSAGES,
  REFUND_METHOD_LABELS,
  REFUND_REASON_LABELS,
} from "./shared";

type Field = "amountPaise" | "reasonCode" | "method" | "upiId" | "complaintId" | "notes";
const FIELDS: readonly Field[] = [
  "amountPaise",
  "reasonCode",
  "method",
  "upiId",
  "complaintId",
  "notes",
];

/** Refusal codes that belong to one field of the request form. */
const CODE_FIELDS: Readonly<Record<string, Field>> = {
  REFUND_EXCEEDS_PAYMENT: "amountPaise",
  NO_CAPTURED_PAYMENT: "amountPaise",
  UPI_ID_REQUIRED: "upiId",
};

const CODE_FALLBACK: Readonly<Record<string, string>> = {
  UPI_ID_REQUIRED: "Enter the customer's UPI ID for a UPI refund.",
};

function RequestRefundDialog({
  orderId,
  open,
  onOpenChange,
}: {
  orderId: Id;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const create = useCreateRefund(orderId);
  const key = useIdempotencyKey();
  const formId = useId();
  const [amount, setAmount] = useState("");
  const [reasonCode, setReasonCode] = useState<RefundReason | "">("");
  const [method, setMethod] = useState<RefundMethod>("ORIGINAL");
  const [upiId, setUpiId] = useState("");
  const [complaintId, setComplaintId] = useState("");
  const [notes, setNotes] = useState("");
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [banner, setBanner] = useState<string | null>(null);

  const close = (next: boolean) => {
    if (!next) {
      setAmount("");
      setReasonCode("");
      setMethod("ORIGINAL");
      setUpiId("");
      setComplaintId("");
      setNotes("");
      setErrors({});
      setBanner(null);
    }
    onOpenChange(next);
  };

  const edit = (apply: () => void) => {
    apply();
    key.reset();
  };

  const submit = () => {
    const amountPaise = parseRupeesToPaise(amount);
    const local: Partial<Record<Field, string>> = {};
    if (amountPaise === null || amountPaise <= 0)
      local.amountPaise = "Enter the refund amount in rupees, e.g. 120 or 120.50.";
    if (!reasonCode) local.reasonCode = "Choose the reason for the refund.";
    if (method === "UPI" && !upiId.trim())
      local.upiId = "Enter the customer's UPI ID for a UPI refund.";
    setErrors(local);
    setBanner(null);
    if (Object.keys(local).length > 0 || amountPaise === null || !reasonCode) return;

    create.mutate(
      {
        body: {
          amountPaise,
          reasonCode,
          method,
          upiId: method === "UPI" ? upiId.trim() : null,
          complaintId: complaintId.trim() || null,
          notes: notes.trim() || null,
        },
        idempotencyKey: key.current(),
      },
      {
        onSuccess: () => {
          key.reset();
          notifySaved(
            "Refund requested",
            "Another staff member with refund approval must approve it.",
          );
          close(false);
        },
        onError: (error) => {
          if (!isApiError(error))
            return reportError(error, { title: "Could not request the refund" });
          const fieldErrors: Partial<Record<Field, string>> = {};
          for (const f of FIELDS) {
            const message = error.fieldMessage(f);
            if (message) fieldErrors[f] = message;
          }
          const codeField = CODE_FIELDS[error.code];
          if (codeField && !fieldErrors[codeField])
            fieldErrors[codeField] =
              PAYMENT_CODE_MESSAGES[error.code] ?? CODE_FALLBACK[error.code] ?? error.message;
          if (Object.keys(fieldErrors).length > 0) setErrors(fieldErrors);
          else if (error.status === 409 || error.status === 422)
            setBanner(PAYMENT_CODE_MESSAGES[error.code] ?? error.message);
          else reportError(error, { title: "Could not request the refund" });
        },
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent
        title="Request refund"
        description="The refund waits for approval by another staff member. The server checks it against what was paid."
        footer={
          <>
            <Button variant="outline" onClick={() => close(false)}>
              Cancel
            </Button>
            <Button type="submit" form={formId} loading={create.isPending}>
              Request refund
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
            submit();
          }}
        >
          {banner && <Banner>{banner}</Banner>}
          <FormField label="Amount (₹)" required error={errors.amountPaise}>
            <Input
              inputMode="decimal"
              value={amount}
              onChange={(e) => edit(() => setAmount(e.target.value))}
              placeholder="0.00"
            />
          </FormField>
          <FormField label="Reason" required error={errors.reasonCode}>
            <Select
              options={REFUND_REASONS.map((r) => ({
                value: r,
                label: REFUND_REASON_LABELS[r] ?? r,
              }))}
              value={reasonCode}
              onValueChange={(v) => edit(() => setReasonCode(v as RefundReason))}
              placeholder="Choose a reason"
            />
          </FormField>
          <FormField label="Refund method" required error={errors.method}>
            <Select
              options={REFUND_METHODS.map((m) => ({
                value: m,
                label: REFUND_METHOD_LABELS[m] ?? m,
              }))}
              value={method}
              onValueChange={(v) => edit(() => setMethod(v as RefundMethod))}
            />
          </FormField>
          {method === "UPI" && (
            <FormField label="Customer UPI ID" required error={errors.upiId}>
              <Input
                value={upiId}
                onChange={(e) => edit(() => setUpiId(e.target.value))}
                placeholder="name@bank"
              />
            </FormField>
          )}
          <FormField
            label="Complaint id"
            error={errors.complaintId}
            hint="Optional: the complaint this refund settles."
          >
            <Input
              value={complaintId}
              onChange={(e) => edit(() => setComplaintId(e.target.value))}
            />
          </FormField>
          <FormField label="Notes" error={errors.notes}>
            <Textarea
              rows={2}
              maxLength={500}
              value={notes}
              onChange={(e) => edit(() => setNotes(e.target.value))}
            />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Payments, refunds and refund requests for one order, shown on the order detail "Payment" tab
 * (`GET /admin/orders/{orderId}/payments`, `POST /admin/orders/{orderId}/refunds`). Owned by the payments
 * feature; the order detail page only places it.
 */
export function OrderPaymentsPanel({ orderId }: { orderId: Id }) {
  const can = useCan();
  const canView = can("payments.view");
  const data = useOrderPayments(orderId, canView);
  const [requesting, setRequesting] = useState(false);

  const requestButton = can("refunds.request") ? (
    <Button variant="outline" onClick={() => setRequesting(true)}>
      Request refund
    </Button>
  ) : null;

  return (
    <div className="flex flex-col gap-6">
      {canView ? (
        <>
          <Section title="Payments" actions={requestButton}>
            {data.isPending ? (
              <Skeleton className="h-16 w-full" />
            ) : data.error ? (
              <ErrorState
                title="Could not load payments"
                traceId={isApiError(data.error) ? data.error.traceId : undefined}
                onRetry={() => void data.refetch()}
              />
            ) : data.data.payments.length === 0 ? (
              <p className="text-text-muted text-sm">No payments on this order yet.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <caption className="sr-only">Payments</caption>
                  <thead className="text-text-muted text-left text-xs uppercase">
                    <tr className="border-border border-b">
                      <th scope="col" className="py-2 pr-3">
                        Method
                      </th>
                      <th scope="col" className="py-2 pr-3">
                        Status
                      </th>
                      <th scope="col" className="py-2 pr-3">
                        Captured / collected
                      </th>
                      <th scope="col" className="py-2 pr-3 text-right">
                        Amount
                      </th>
                      <th scope="col" className="py-2 text-right">
                        Refundable
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.data.payments.map((p) => {
                      const at = p.capturedAt ?? p.collectedAt;
                      return (
                        <tr key={p.id} className="border-border border-b last:border-0">
                          <td className="py-2 pr-3">
                            <Link
                              to={`/payments/${p.id}`}
                              className="text-accent-text hover:underline"
                            >
                              {METHOD_LABELS[p.method] ?? p.method}
                            </Link>
                          </td>
                          <td className="py-2 pr-3">
                            <StatusBadge code={p.status} map={PAYMENT_STATUS} />
                          </td>
                          <td className="py-2 pr-3">{at ? formatDateTime(at) : "—"}</td>
                          <td className="py-2 pr-3 text-right tabular-nums">
                            {formatRupees(p.amountPaise)}
                          </td>
                          <td className="py-2 text-right tabular-nums">
                            {formatRupees(p.summary.refundablePaise)}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Section>
          <Section title="Refunds">
            <RefundsTable
              refunds={data.data?.refunds ?? []}
              isLoading={data.isPending}
              error={data.error}
              onRetry={() => void data.refetch()}
            />
          </Section>
        </>
      ) : (
        requestButton && <div>{requestButton}</div>
      )}
      <RequestRefundDialog orderId={orderId} open={requesting} onOpenChange={setRequesting} />
    </div>
  );
}
