import { formatDateTime, formatRupees, humanizeCode } from "@fresh/core";
import { Badge, StatusBadge } from "@fresh/ui";
import { Link, useParams } from "react-router";
import { useOrderPayments, usePayment } from "../../api/payments";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { PAYMENT_STATUS } from "../../lib/status";
import { RefundsTable } from "./RefundsTable";
import { METHOD_LABELS } from "./shared";
import { SummaryTiles } from "./SummaryTiles";

export function PaymentDetailPage() {
  const { paymentId = "" } = useParams();
  const payment = usePayment(paymentId);
  const orderPayments = useOrderPayments(payment.data?.orderId ?? "", Boolean(payment.data));

  return (
    <QueryView query={payment} what="payment">
      {(p) => (
        <>
          <PageHeader
            title={`Payment ${p.providerPaymentId ?? p.id.slice(-8)}`}
            backTo="/payments"
            backLabel="Payments"
            meta={
              <>
                <StatusBadge code={p.status} map={PAYMENT_STATUS} />
                {p.isExcess && <Badge tone="warning">Excess payment</Badge>}
              </>
            }
          />
          <div className="flex flex-col gap-6">
            <SummaryTiles summary={p.summary} />
            <Section title="Details">
              <DetailList
                columns={3}
                items={[
                  {
                    label: "Order",
                    value: (
                      <Link
                        to={`/orders/${p.orderId}`}
                        className="text-accent-text hover:underline"
                      >
                        View order
                      </Link>
                    ),
                  },
                  { label: "Method", value: METHOD_LABELS[p.method] ?? p.method },
                  { label: "Provider", value: p.provider },
                  { label: "Amount", value: formatRupees(p.amountPaise) },
                  { label: "Provider order", value: p.providerOrderId },
                  { label: "Provider payment", value: p.providerPaymentId },
                  { label: "Captured", value: p.capturedAt ? formatDateTime(p.capturedAt) : null },
                  {
                    label: "Collected",
                    value:
                      p.collectedPaise != null
                        ? `${formatRupees(p.collectedPaise)}${p.collectionMode ? ` · ${humanizeCode(p.collectionMode)}` : ""}${
                            p.collectedAt ? ` · ${formatDateTime(p.collectedAt)}` : ""
                          }`
                        : null,
                  },
                  { label: "Collection reference", value: p.collectionReference },
                  { label: "Expires", value: p.expiresAt ? formatDateTime(p.expiresAt) : null },
                  {
                    label: "Delivered",
                    value: p.orderDeliveredAt ? formatDateTime(p.orderDeliveredAt) : null,
                  },
                  {
                    label: "Failure",
                    value: p.failureCode
                      ? `${humanizeCode(p.failureCode)}${p.failureMessage ? ` — ${p.failureMessage}` : ""}`
                      : null,
                  },
                ]}
              />
            </Section>
            <Section title="Refunds on this order">
              <RefundsTable
                refunds={orderPayments.data?.refunds ?? []}
                isLoading={orderPayments.isPending && Boolean(payment.data)}
                error={orderPayments.error}
                onRetry={() => void orderPayments.refetch()}
              />
            </Section>
          </div>
        </>
      )}
    </QueryView>
  );
}
