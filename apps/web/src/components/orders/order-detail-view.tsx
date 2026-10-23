"use client";

import {
  formatDateTime,
  formatDayShort,
  formatQuantity,
  formatRupees,
  formatTime,
} from "@fresh/core";
import { Card, CardContent, ErrorState, Skeleton, StatusBadge } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { KeyRound, Phone } from "lucide-react";
import { browserHttp } from "@/api/browser";
import { getOrder, getTracking, listComplaints } from "@/api/endpoints/orders";
import { type OrderAmounts, type OrderDetail, type OrderTracking } from "@/api/types/orders";
import { cx } from "@/lib/cx";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { complaintStatusMap, orderStatusMap, paymentMethodLabels } from "@/lib/status-maps";
import { OrderActions } from "./order-actions";

const ITEM_STATUS_TEXT: Record<string, string> = {
  SHORT_SUPPLIED: "Short supplied",
  SUBSTITUTED: "Substituted",
  CANCELLED: "Removed",
};

function Amounts({ amounts, final }: { amounts: OrderAmounts; final: boolean }) {
  const rows: [string, number][] = [
    ["Items", amounts.itemsPaise],
    ["Cleaning and cutting", amounts.preparationPaise],
  ];
  return (
    <dl className="flex flex-col gap-2 text-sm">
      {rows
        .filter(([, v]) => v > 0)
        .map(([label, value]) => (
          <div key={label} className="text-text flex justify-between">
            <dt>{label}</dt>
            <dd className="tabular-nums">{formatRupees(value)}</dd>
          </div>
        ))}
      {amounts.discountPaise > 0 && (
        <div className="text-text flex justify-between">
          <dt>Discount</dt>
          <dd className="text-success-text tabular-nums">
            − {formatRupees(amounts.discountPaise)}
          </dd>
        </div>
      )}
      <div className="text-text flex justify-between">
        <dt>Delivery fee</dt>
        <dd className="tabular-nums">
          {amounts.deliveryFeePaise === 0 ? "Free" : formatRupees(amounts.deliveryFeePaise)}
        </dd>
      </div>
      <div className="border-border text-text mt-1 flex justify-between border-t pt-2 text-base font-bold">
        <dt>{final ? "Total" : "Estimated total"}</dt>
        <dd className="tabular-nums">{formatRupees(amounts.totalPaise)}</dd>
      </div>
      {amounts.taxPaise > 0 && (
        <p className="text-text-muted text-xs">
          Includes taxes of {formatRupees(amounts.taxPaise)}.
        </p>
      )}
    </dl>
  );
}

function Timeline({ tracking }: { tracking: OrderTracking }) {
  return (
    <ol className="flex flex-col gap-0" aria-label="Order progress">
      {tracking.timeline.map((event, i) => {
        const last = i === tracking.timeline.length - 1;
        return (
          <li key={`${event.status}-${event.at}`} className="flex gap-3">
            <div className="flex flex-col items-center">
              <span
                aria-hidden="true"
                className={cx(
                  "mt-1 size-3 rounded-full",
                  last ? "bg-accent ring-accent-subtle ring-4" : "bg-border-strong",
                )}
              />
              {!last && <span aria-hidden="true" className="bg-border w-px flex-1" />}
            </div>
            <div className="flex flex-col pb-5">
              <span className={cx("text-sm", last ? "text-text font-semibold" : "text-text")}>
                {orderStatusMap[event.status]?.label ?? event.status}
              </span>
              <span className="text-text-muted text-xs">{formatDateTime(event.at)}</span>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

function Items({ order }: { order: OrderDetail }) {
  return (
    <ul className="divide-border flex flex-col divide-y">
      {order.items.map((item) => (
        <li key={item.id} className="flex flex-wrap justify-between gap-2 py-3 text-sm">
          <div className="flex flex-col">
            <span className="text-text font-medium">{item.productName}</span>
            <span className="text-text-muted">
              {item.variantName} · {item.preparationName}
            </span>
            <span className="text-text-muted">
              Ordered {formatQuantity(item.orderedQuantity)}
              {item.chargedQuantity && ` · charged for ${formatQuantity(item.chargedQuantity)}`}
            </span>
            {ITEM_STATUS_TEXT[item.status] && (
              <span className="text-warning-text">{ITEM_STATUS_TEXT[item.status]}</span>
            )}
          </div>
          <div className="text-right">
            {item.finalTotalPaise !== null ? (
              <span className="text-text font-semibold tabular-nums">
                {formatRupees(item.finalTotalPaise)}
              </span>
            ) : (
              <span className="text-text tabular-nums">
                est. {formatRupees(item.estimatedTotalPaise)}
              </span>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function OrderDetailView({ orderId }: { orderId: string }) {
  const order = useQuery({
    queryKey: queryKeys.order(orderId),
    queryFn: () => getOrder(browserHttp(), orderId),
  });
  const tracking = useQuery({
    queryKey: queryKeys.tracking(orderId),
    queryFn: () => getTracking(browserHttp(), orderId),
    // Keep the delivery code and rider status fresh while the order is on its way.
    refetchInterval: (q) =>
      q.state.data && !["DELIVERED", "CANCELLED", "RETURNED"].includes(q.state.data.status)
        ? 60_000
        : false,
  });
  const complaints = useQuery({
    queryKey: queryKeys.complaints(orderId),
    queryFn: () => listComplaints(browserHttp(), orderId),
    enabled: order.data?.status === "DELIVERED",
  });

  if (order.isPending) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-48 rounded-xl" />
        <Skeleton className="h-48 rounded-xl" />
      </div>
    );
  }
  if (order.isError) {
    const notFound = asApiError(order.error)?.status === 404;
    return (
      <ErrorState
        title={notFound ? "Order not found" : "We could not load this order"}
        description={
          notFound ? "Check the link or open it from your orders." : describeError(order.error)
        }
        traceId={asApiError(order.error)?.traceId}
        {...(notFound ? {} : { onRetry: () => void order.refetch() })}
      />
    );
  }

  const o = order.data;
  const t = tracking.data;
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-text text-3xl font-bold">Order {o.orderNumber}</h1>
          <p className="text-text-muted text-sm">Placed {formatDateTime(o.placedAt)}</p>
        </div>
        <StatusBadge code={o.status} map={orderStatusMap} />
      </div>

      {t?.deliveryCode && (
        <div className="bg-primary text-on-primary flex items-center gap-4 rounded-2xl p-5">
          <KeyRound aria-hidden="true" className="size-8 shrink-0" />
          <div>
            <p className="text-sm opacity-90">Share this code with the delivery partner</p>
            <p className="[font-family:var(--fo-font-display)] text-3xl font-bold tracking-[0.3em]">
              {t.deliveryCode}
            </p>
          </div>
        </div>
      )}

      <OrderActions order={o} />

      <div className="grid items-start gap-6 lg:grid-cols-[1fr_22rem]">
        <div className="flex flex-col gap-6">
          <Card>
            <CardContent className="flex flex-col gap-4">
              <h2 className="text-text text-lg font-semibold">Delivery</h2>
              <p className="text-text text-sm">
                {formatDayShort(o.slot.startAt)}, {formatTime(o.slot.startAt)} –{" "}
                {formatTime(o.slot.endAt)}
              </p>
              <address className="text-text-muted text-sm not-italic">
                {o.address.recipientName}, {o.address.line1}
                {o.address.line2 && `, ${o.address.line2}`}, {o.address.locality},{" "}
                {o.address.cityName} {o.address.pincode}
              </address>
              {t?.shipment?.riderName && (
                <p className="text-text flex items-center gap-2 text-sm">
                  <Phone aria-hidden="true" className="size-4" />
                  {t.shipment.riderName}
                  {t.shipment.riderPhone && (
                    <a
                      href={`tel:${t.shipment.riderPhone}`}
                      className="text-accent-text font-semibold underline-offset-4 hover:underline"
                    >
                      Call
                    </a>
                  )}
                </p>
              )}
              {tracking.isPending ? (
                <Skeleton className="h-24" />
              ) : t ? (
                <Timeline tracking={t} />
              ) : (
                <p className="text-text-muted text-sm">Tracking is not available right now.</p>
              )}
            </CardContent>
          </Card>
          <Card>
            <CardContent className="flex flex-col gap-2">
              <h2 className="text-text text-lg font-semibold">Items</h2>
              <Items order={o} />
            </CardContent>
          </Card>
          {complaints.data && complaints.data.length > 0 && (
            <Card>
              <CardContent className="flex flex-col gap-3">
                <h2 className="text-text text-lg font-semibold">Your complaints</h2>
                <ul className="flex flex-col gap-2">
                  {complaints.data.map((c) => (
                    <li key={c.id} className="flex items-start justify-between gap-3 text-sm">
                      <span className="text-text">{c.description}</span>
                      <StatusBadge code={c.status} map={complaintStatusMap} />
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}
        </div>

        <Card className="lg:sticky lg:top-24">
          <CardContent className="flex flex-col gap-4">
            <h2 className="text-text text-lg font-semibold">Payment</h2>
            <p className="text-text text-sm">
              {paymentMethodLabels[o.paymentMethod] ?? o.paymentMethod}
            </p>
            <Amounts amounts={o.final ?? o.estimate} final={o.final !== null} />
            {o.final === null && (
              <p className="text-text-muted text-xs">
                The final amount is confirmed when your order is packed.
              </p>
            )}
            {o.amountDuePaise > 0 && (
              <p className="bg-accent-subtle text-accent-text rounded-lg p-3 text-sm font-semibold">
                To pay on delivery: {formatRupees(o.amountDuePaise)}
              </p>
            )}
            {o.amountRefundedPaise > 0 && (
              <p className="text-text-muted text-sm">
                Refunded: {formatRupees(o.amountRefundedPaise)}
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
