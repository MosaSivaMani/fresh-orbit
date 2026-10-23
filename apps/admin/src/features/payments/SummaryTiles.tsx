import { formatRupees } from "@fresh/core";
import { type PaymentSummary } from "../../api/types/payments";

/** The payment summary exactly as the server computed it (paid, refunded, pending, refundable). */
export function SummaryTiles({ summary }: { summary: PaymentSummary }) {
  const tiles = [
    { label: "Paid / collected", value: summary.paidPaise },
    { label: "Refunded", value: summary.refundedPaise },
    { label: "Refunds pending", value: summary.pendingRefundPaise },
    { label: "Still refundable", value: summary.refundablePaise },
  ];
  return (
    <dl className="grid grid-cols-2 gap-3 md:grid-cols-4">
      {tiles.map((t) => (
        <div key={t.label} className="border-border bg-surface rounded-lg border p-4 shadow-xs">
          <dt className="text-text-muted text-sm font-medium">{t.label}</dt>
          <dd className="text-xl leading-7 font-semibold tabular-nums">{formatRupees(t.value)}</dd>
        </div>
      ))}
    </dl>
  );
}
