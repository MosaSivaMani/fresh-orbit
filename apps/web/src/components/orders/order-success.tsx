"use client";

import { formatDayShort, formatRupees, formatTime } from "@fresh/core";
import { Button, Card, CardContent, ErrorState, Skeleton } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { CircleCheck } from "lucide-react";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { getOrder } from "@/api/endpoints/orders";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { paymentMethodLabels } from "@/lib/status-maps";

export function OrderSuccess({ orderId }: { orderId: string }) {
  const order = useQuery({
    queryKey: queryKeys.order(orderId),
    queryFn: () => getOrder(browserHttp(), orderId),
  });

  if (order.isPending) return <Skeleton className="mx-auto h-80 max-w-xl rounded-2xl" />;
  if (order.isError) {
    return (
      <ErrorState
        title="We could not load your order"
        description={describeError(order.error)}
        traceId={asApiError(order.error)?.traceId}
        onRetry={() => void order.refetch()}
      />
    );
  }
  const o = order.data;

  return (
    <Card className="mx-auto w-full max-w-xl">
      <CardContent className="flex flex-col items-center gap-6 p-8 text-center">
        <CircleCheck aria-hidden="true" className="text-success size-14" />
        <div className="flex flex-col gap-1">
          <h1 className="text-text text-2xl font-bold">Order placed</h1>
          <p className="text-text-muted">
            Order <span className="text-text font-semibold">{o.orderNumber}</span>. We will send
            updates by SMS.
          </p>
        </div>
        <dl className="border-border grid w-full gap-3 rounded-xl border p-4 text-left text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-text-muted">Delivery</dt>
            <dd className="text-text font-medium">
              {formatDayShort(o.slot.startAt)}, {formatTime(o.slot.startAt)} –{" "}
              {formatTime(o.slot.endAt)}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-text-muted">Payment</dt>
            <dd className="text-text font-medium">
              {paymentMethodLabels[o.paymentMethod] ?? o.paymentMethod}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-text-muted">Estimated total</dt>
            <dd className="text-text font-bold">{formatRupees(o.estimate.totalPaise)}</dd>
          </div>
        </dl>
        <p className="text-text-muted text-xs">
          Weighed items are charged on actual net weight; the final amount is confirmed when your
          order is packed.
        </p>
        <div className="flex w-full flex-col gap-2 sm:flex-row sm:justify-center">
          <Button asChild>
            <Link href={`/orders/${encodeURIComponent(o.id)}`}>Track order</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/shop">Continue shopping</Link>
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
