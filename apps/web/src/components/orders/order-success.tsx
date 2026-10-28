"use client";

import { formatRupees } from "@fresh/core";
import { BillBox, Button, CharacterState, Script, Skeleton } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { getOrder } from "@/api/endpoints/orders";
import { LoadError } from "@/components/states";
import { queryKeys } from "@/lib/query-keys";
import { slotText } from "@/lib/slot-text";
import { paymentMethodLabels } from "@/lib/status-maps";
import { amountRows } from "./order-detail-view";

/** The order-placed screen (D95): the ORDER_PLACED character, the server's slot, payment and estimate. */
export function OrderSuccess({ orderId }: { orderId: string }) {
  const order = useQuery({
    queryKey: queryKeys.order(orderId),
    queryFn: () => getOrder(browserHttp(), orderId),
  });

  if (order.isPending) return <Skeleton className="mx-auto h-96 max-w-xl rounded-[22px]" />;
  if (order.isError) {
    return (
      <LoadError
        title="We could not load your order"
        error={order.error}
        headingLevel={1}
        onRetry={() => void order.refetch()}
      />
    );
  }
  const o = order.data;

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col gap-5">
      <CharacterState
        slot="ORDER_PLACED"
        avatarSize="md"
        headingLevel={1}
        className="pb-2"
        title={
          <>
            Order <Script tone="accent">placed!</Script>
          </>
        }
        description={
          <>
            Order <span className="text-text font-bold">{o.orderNumber}</span>. We will send updates
            by SMS.
          </>
        }
      />
      <dl className="bg-surface-paper border-border grid gap-3 rounded-[20px] border p-4 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-text-muted">Delivery</dt>
          <dd className="text-text text-right font-bold">{slotText(o.slot, o.timeZone)}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-text-muted">Payment</dt>
          <dd className="text-text font-bold">
            {paymentMethodLabels[o.paymentMethod] ?? o.paymentMethod}
          </dd>
        </div>
      </dl>
      <BillBox
        title="Bill"
        rows={amountRows(o.estimate)}
        total={{ label: "Estimated total", value: formatRupees(o.estimate.totalPaise) }}
        notes={
          <p>
            Weighed items are charged on actual net weight; the final amount is confirmed when your
            order is packed.
          </p>
        }
      />
      <div className="flex flex-col gap-2 sm:flex-row sm:justify-center">
        <Button asChild size="lg">
          <Link href={`/orders/${encodeURIComponent(o.id)}`}>Track order</Link>
        </Button>
        <Button asChild variant="outline" size="lg">
          <Link href="/shop">Continue shopping</Link>
        </Button>
      </div>
    </div>
  );
}
