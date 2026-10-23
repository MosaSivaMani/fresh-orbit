import { isApiError, formatDateTime, formatRupees } from "@fresh/core";
import { ErrorState, Skeleton, StatusBadge } from "@fresh/ui";
import { Link } from "react-router";
import { type Refund } from "../../api/types/payments";
import { REFUND_STATUS } from "../../lib/status";
import { REFUND_METHOD_LABELS, REFUND_REASON_LABELS } from "./shared";

/** Small, unpaged refunds table for one order or payment. */
export function RefundsTable({
  refunds,
  isLoading,
  error,
  onRetry,
}: {
  refunds: readonly Refund[];
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
}) {
  if (isLoading) return <Skeleton className="h-16 w-full" />;
  if (error)
    return (
      <ErrorState
        title="Could not load refunds"
        traceId={isApiError(error) ? error.traceId : undefined}
        onRetry={onRetry}
      />
    );
  if (refunds.length === 0) return <p className="text-text-muted text-sm">No refunds.</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="sr-only">Refunds</caption>
        <thead className="text-text-muted text-left text-xs uppercase">
          <tr className="border-border border-b">
            <th scope="col" className="py-2 pr-3">
              Requested
            </th>
            <th scope="col" className="py-2 pr-3">
              Reason
            </th>
            <th scope="col" className="py-2 pr-3">
              Method
            </th>
            <th scope="col" className="py-2 pr-3">
              Status
            </th>
            <th scope="col" className="py-2 text-right">
              Amount
            </th>
          </tr>
        </thead>
        <tbody>
          {refunds.map((r) => (
            <tr key={r.id} className="border-border border-b last:border-0">
              <td className="py-2 pr-3">
                <Link to={`/refunds/${r.id}`} className="text-accent-text hover:underline">
                  {formatDateTime(r.requestedAt)}
                </Link>
              </td>
              <td className="py-2 pr-3">{REFUND_REASON_LABELS[r.reasonCode] ?? r.reasonCode}</td>
              <td className="py-2 pr-3">{REFUND_METHOD_LABELS[r.method] ?? r.method}</td>
              <td className="py-2 pr-3">
                <StatusBadge code={r.status} map={REFUND_STATUS} />
              </td>
              <td className="py-2 text-right tabular-nums">{formatRupees(r.amountPaise)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
