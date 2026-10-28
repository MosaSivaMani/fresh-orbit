"use client";

import { formatDateTime, formatQuantity, formatRupees } from "@fresh/core";
import { Button, StepCard, toast } from "@fresh/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { answerSubstitution } from "@/api/endpoints/orders";
import { type OrderDetail } from "@/api/types/orders";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

type Substitution = OrderDetail["substitutions"][number];

const STATUS_LABELS: Record<string, string> = {
  PROPOSED: "Waiting for your answer",
  ACCEPTED: "Accepted",
  REJECTED: "Declined",
  EXPIRED: "Expired",
};

/** The server's price difference as sent: "+₹40.00", "−₹12.00" or "Same price". */
function difference(paise: number): string {
  if (paise === 0) return "Same price";
  return `${paise > 0 ? "+" : "−"}${formatRupees(Math.abs(paise))}`;
}

/**
 * Substitutions the store proposed for items it could not supply (ORD substitutions). A PROPOSED one shows Accept
 * and Reject; the server answers with the re-priced order, which replaces the cached one. Nothing is priced here.
 */
export function SubstitutionsCard({ order }: { order: OrderDetail }) {
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<string | null>(null);
  if (order.substitutions.length === 0) return null;

  const itemName = (s: Substitution) =>
    order.items.find((i) => i.id === s.orderItemId)?.productName ?? "An item";

  async function answer(s: Substitution, choice: "accept" | "reject") {
    setPending(`${s.id}:${choice}`);
    try {
      const updated = await answerSubstitution(browserHttp(), order.id, s.id, choice);
      queryClient.setQueryData(queryKeys.order(order.id), updated);
      void queryClient.invalidateQueries({ queryKey: queryKeys.tracking(order.id) });
      toast({
        title: choice === "accept" ? "Substitution accepted" : "Substitution declined",
        tone: "success",
      });
    } catch (e) {
      toast({ title: describeError(e), tone: "danger" });
      // A refusal (409/422: the offer expired or was already answered, the order moved on) means our copy is
      // stale; read the order again so the card shows the server's state.
      if (asApiError(e)?.status !== 0) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.order(order.id) });
      }
    } finally {
      setPending(null);
    }
  }

  return (
    <StepCard title="Substitutions">
      <ul className="flex flex-col gap-3">
        {order.substitutions.map((s) => (
          <li
            key={s.id}
            className="bg-surface-sand flex flex-col gap-2 rounded-[20px] p-4 text-sm sm:flex-row sm:items-center sm:justify-between"
          >
            <div className="flex min-w-0 flex-col gap-0.5">
              <p className="text-text-muted">Instead of {itemName(s)}</p>
              <p className="text-text font-semibold">
                {s.proposedName} · {formatQuantity(s.proposedQuantity)}
              </p>
              <p className="text-text-muted tabular-nums">
                {difference(s.priceDifferencePaise)}
                {s.status === "PROPOSED" &&
                  ` · answer by ${formatDateTime(s.expiresAt, order.timeZone)}`}
              </p>
            </div>
            {s.status === "PROPOSED" ? (
              <div className="flex shrink-0 gap-2">
                <Button
                  size="sm"
                  loading={pending === `${s.id}:accept`}
                  disabled={pending !== null}
                  onClick={() => void answer(s, "accept")}
                  aria-label={`Accept ${s.proposedName}`}
                >
                  Accept
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  loading={pending === `${s.id}:reject`}
                  disabled={pending !== null}
                  onClick={() => void answer(s, "reject")}
                  aria-label={`Reject ${s.proposedName}`}
                >
                  Reject
                </Button>
              </div>
            ) : (
              <p className="text-text shrink-0 font-semibold">
                {STATUS_LABELS[s.status] ?? s.status}
              </p>
            )}
          </li>
        ))}
      </ul>
    </StepCard>
  );
}
