import { formatDateTime, formatRupees, isApiError } from "@fresh/core";
import { Button, StatusBadge } from "@fresh/ui";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { useRefund, useRefundAction } from "../../api/payments";
import { type Refund } from "../../api/types/payments";
import { useCan } from "../../app/services";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { REFUND_STATUS } from "../../lib/status";
import { SummaryTiles } from "./SummaryTiles";
import {
  Banner,
  PAYMENT_CODE_MESSAGES,
  REFUND_METHOD_LABELS,
  REFUND_REASON_LABELS,
  TextCommandDialog,
  codeMessage,
} from "./shared";

function RefundActions({ refund, reload }: { refund: Refund; reload: () => unknown }) {
  const can = useCan();
  const action = useRefundAction(refund.id);
  const [dialog, setDialog] = useState<"reject" | "mark-paid" | null>(null);
  const [banner, setBanner] = useState<string | null>(null);

  if (!can("refunds.approve")) return null;
  const awaitingApproval = refund.status === "REQUESTED";
  // Manual payouts (UPI, bank transfer, cash) are marked paid by staff; ORIGINAL refunds go through the provider.
  const manualPayout =
    (refund.status === "APPROVED" || refund.status === "PROCESSING") &&
    refund.method !== "ORIGINAL";

  const approve = () => {
    setBanner(null);
    action.mutate(
      { action: "approve", version: refund.version },
      {
        onSuccess: () => notifySaved("Refund approved"),
        onError: (error) => {
          if (
            isApiError(error) &&
            (PAYMENT_CODE_MESSAGES[error.code] || error.status === 422 || error.status === 409)
          )
            setBanner(codeMessage(error));
          else reportError(error, { onConflict: reload, title: "Could not approve the refund" });
        },
      },
    );
  };

  if (!awaitingApproval && !manualPayout) return null;
  return (
    <div className="flex flex-col gap-3">
      {banner && <Banner>{banner}</Banner>}
      <div className="flex flex-wrap gap-2">
        {awaitingApproval && (
          <>
            <Button
              onClick={approve}
              loading={action.isPending && action.variables?.action === "approve"}
            >
              Approve refund
            </Button>
            <Button variant="outline" onClick={() => setDialog("reject")}>
              Reject
            </Button>
          </>
        )}
        {manualPayout && <Button onClick={() => setDialog("mark-paid")}>Mark paid</Button>}
      </div>
      <TextCommandDialog
        open={dialog === "reject"}
        onOpenChange={(open) => setDialog(open ? "reject" : null)}
        title="Reject refund"
        description="The customer is not refunded. The reason is kept on the refund."
        field="reason"
        label="Reason"
        required
        maxLength={500}
        submitLabel="Reject refund"
        danger
        onConflict={reload}
        onSubmit={async (reason) => {
          await action.mutateAsync({ action: "reject", version: refund.version, body: { reason } });
          notifySaved("Refund rejected");
        }}
      />
      <TextCommandDialog
        open={dialog === "mark-paid"}
        onOpenChange={(open) => setDialog(open ? "mark-paid" : null)}
        title="Mark refund paid"
        description="Record the payout reference (UPI transaction id, bank reference or cash receipt number)."
        field="reference"
        label="Reference"
        hint="4 to 64 characters."
        required
        minLength={4}
        maxLength={64}
        multiline={false}
        submitLabel="Mark paid"
        onConflict={reload}
        onSubmit={async (reference) => {
          await action.mutateAsync({
            action: "mark-paid",
            version: refund.version,
            body: { reference },
          });
          notifySaved("Refund marked paid");
        }}
      />
    </div>
  );
}

export function RefundDetailPage() {
  const { refundId = "" } = useParams();
  const refund = useRefund(refundId);

  return (
    <QueryView query={refund} what="refund">
      {(r) => (
        <>
          <PageHeader
            title={`Refund of ${formatRupees(r.amountPaise)}`}
            backTo="/refunds"
            backLabel="Refunds"
            meta={<StatusBadge code={r.status} map={REFUND_STATUS} />}
          />
          <div className="flex flex-col gap-6">
            <RefundActions refund={r} reload={() => refund.refetch()} />
            <Section title="Order payment summary">
              <SummaryTiles summary={r.paymentSummary} />
            </Section>
            <Section title="Details">
              <DetailList
                columns={3}
                items={[
                  {
                    label: "Order",
                    value: (
                      <Link
                        to={`/orders/${r.orderId}`}
                        className="text-accent-text hover:underline"
                      >
                        View order
                      </Link>
                    ),
                  },
                  {
                    label: "Payment",
                    value: (
                      <Link
                        to={`/payments/${r.paymentId}`}
                        className="text-accent-text hover:underline"
                      >
                        View payment
                      </Link>
                    ),
                  },
                  { label: "Amount", value: formatRupees(r.amountPaise) },
                  { label: "Reason", value: REFUND_REASON_LABELS[r.reasonCode] ?? r.reasonCode },
                  { label: "Method", value: REFUND_METHOD_LABELS[r.method] ?? r.method },
                  { label: "Payee UPI ID", value: r.payeeUpiId },
                  { label: "Requested", value: formatDateTime(r.requestedAt) },
                  { label: "Approved", value: r.approvedAt ? formatDateTime(r.approvedAt) : null },
                  { label: "Paid", value: r.succeededAt ? formatDateTime(r.succeededAt) : null },
                  { label: "Provider refund", value: r.providerRefundId },
                  { label: "Manual reference", value: r.manualReference },
                  { label: "Attempts", value: String(r.attemptCount) },
                  { label: "Notes", value: r.notes },
                  { label: "Failure", value: r.failureReason },
                ]}
              />
            </Section>
          </div>
        </>
      )}
    </QueryView>
  );
}
