"use client";

import {
  formatDateTime,
  formatDayShort,
  formatQuantity,
  formatRupees,
  formatTime,
} from "@fresh/core";
import {
  AvatarCircle,
  BillBox,
  type BillRow,
  PageBand,
  Script,
  Skeleton,
  StatusBadge,
  StatusSteps,
  StepCard,
} from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { KeyRound, Phone } from "lucide-react";
import { type ReactNode } from "react";
import { browserHttp } from "@/api/browser";
import { getOrder, getTracking, listComplaints } from "@/api/endpoints/orders";
import { type OrderAmounts, type OrderDetail } from "@/api/types/orders";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { Container } from "@/components/section";
import { LoadError } from "@/components/states";
import { asApiError } from "@/lib/errors";
import { orderSlot, orderSteps } from "@/lib/order-progress";
import { queryKeys } from "@/lib/query-keys";
import { complaintStatusMap, orderStatusMap, paymentMethodLabels } from "@/lib/status-maps";
import { OrderActions } from "./order-actions";

const ITEM_STATUS_TEXT: Record<string, string> = {
  SHORT_SUPPLIED: "Short supplied",
  SUBSTITUTED: "Substituted",
  CANCELLED: "Removed",
};

const CLOSED = ["DELIVERED", "CANCELLED", "RETURNED"];

/** The server's amounts as bill rows; the total is the server's `totalPaise`, never a sum. */
export function amountRows(amounts: OrderAmounts): BillRow[] {
  const rows: BillRow[] = [
    { id: "items", label: "Items", value: formatRupees(amounts.itemsPaise) },
  ];
  if (amounts.preparationPaise > 0) {
    rows.push({
      id: "prep",
      label: "Cleaning and cutting",
      value: formatRupees(amounts.preparationPaise),
    });
  }
  if (amounts.discountPaise > 0) {
    rows.push({
      id: "discount",
      label: "Discount",
      value: `− ${formatRupees(amounts.discountPaise)}`,
      tone: "saving",
    });
  }
  rows.push({
    id: "delivery",
    label: "Delivery fee",
    value: amounts.deliveryFeePaise === 0 ? "Free" : formatRupees(amounts.deliveryFeePaise),
  });
  return rows;
}

function Items({ order }: { order: OrderDetail }) {
  return (
    <ul className="divide-border flex flex-col divide-y divide-dashed">
      {order.items.map((item) => (
        <li key={item.id} className="flex justify-between gap-3 py-3 text-sm first:pt-0 last:pb-0">
          <div className="flex min-w-0 flex-col">
            <span className="text-text font-bold">{item.productName}</span>
            <span className="text-text-muted">
              {item.variantName} · {item.preparationName}
            </span>
            <span className="text-text-muted">
              Ordered {formatQuantity(item.orderedQuantity)}
              {item.chargedQuantity && ` · charged for ${formatQuantity(item.chargedQuantity)}`}
            </span>
            {ITEM_STATUS_TEXT[item.status] && (
              <span className="text-warning-text font-semibold">
                {ITEM_STATUS_TEXT[item.status]}
              </span>
            )}
          </div>
          <div className="shrink-0 text-right">
            {item.finalTotalPaise !== null ? (
              <span className="font-display text-base font-extrabold tabular-nums">
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

function Band({ order, aside }: { order?: OrderDetail; aside?: ReactNode }) {
  return (
    <PageBand
      eyebrow={order ? `Placed ${formatDateTime(order.placedAt)}` : undefined}
      title={
        order ? (
          <>
            Order <Script>{order.orderNumber}</Script>
          </>
        ) : (
          <>
            Your <Script>order</Script>
          </>
        )
      }
      aside={aside}
    />
  );
}

/** Order tracking (D95): status steps, delivery code when sent, rider, items, the bill and the allowed actions. */
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
      q.state.data && !CLOSED.includes(q.state.data.status) ? 60_000 : false,
  });
  const complaints = useQuery({
    queryKey: queryKeys.complaints(orderId),
    queryFn: () => listComplaints(browserHttp(), orderId),
    enabled: order.data?.status === "DELIVERED",
  });

  const crumbs = (
    <Breadcrumbs
      items={[
        { href: "/account", label: "Account" },
        { href: "/account/orders", label: "Orders" },
        { label: order.data?.orderNumber ?? "Order" },
      ]}
    />
  );

  if (order.isPending) {
    return (
      <>
        <Band />
        <Container className="flex flex-col gap-4 py-6" aria-busy="true">
          <Skeleton className="h-28 rounded-[22px]" />
          <Skeleton className="h-48 rounded-[22px]" />
        </Container>
      </>
    );
  }
  if (order.isError) {
    const notFound = asApiError(order.error)?.status === 404;
    return (
      <>
        <Band />
        <Container className="py-6">
          <LoadError
            title={notFound ? "Order not found" : "We could not load this order"}
            error={order.error}
            {...(notFound
              ? {
                  slot: "NOT_FOUND" as const,
                  description: "Check the link or open it from your orders.",
                }
              : { onRetry: () => void order.refetch() })}
          />
        </Container>
      </>
    );
  }

  const o = order.data;
  const t = tracking.data;
  const status = t?.status ?? o.status;
  const amounts = o.final ?? o.estimate;
  return (
    <>
      <Band
        order={o}
        aside={
          <div className="flex items-center gap-3">
            <StatusBadge code={o.status} map={orderStatusMap} />
            <AvatarCircle slot={orderSlot(status)} size="xs" />
          </div>
        }
      />
      <Container className="flex flex-col gap-5 py-6 sm:py-8">
        {crumbs}

        {t?.deliveryCode && (
          <div className="bg-block-teal text-on-block-teal flex items-center gap-4 rounded-[22px] p-5">
            <KeyRound aria-hidden="true" className="size-8 shrink-0" />
            <div>
              <p className="text-sm font-semibold">Share this code with the delivery partner</p>
              <p className="font-display text-3xl font-extrabold tracking-[0.3em]">
                {t.deliveryCode}
              </p>
            </div>
          </div>
        )}

        <div className="grid items-start gap-6 lg:grid-cols-[1fr_24rem]">
          <div className="flex min-w-0 flex-col gap-4">
            <StepCard title="Tracking">
              {tracking.isPending ? (
                <Skeleton className="h-16" />
              ) : t ? (
                <StatusSteps steps={orderSteps(t.status, t.timeline)} />
              ) : (
                <p className="text-text-muted text-sm">Tracking is not available right now.</p>
              )}
              <div className="bg-surface-sand grid gap-1 rounded-2xl p-3 text-sm">
                <p className="text-text font-bold">
                  {formatDayShort(o.slot.startAt)}, {formatTime(o.slot.startAt)} –{" "}
                  {formatTime(o.slot.endAt)}
                </p>
                <address className="text-text-muted not-italic">
                  {o.address.recipientName}, {o.address.line1}
                  {o.address.line2 && `, ${o.address.line2}`}, {o.address.locality},{" "}
                  {o.address.cityName} {o.address.pincode}
                </address>
                {t?.shipment?.riderName && (
                  <p className="text-text flex items-center gap-2">
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
              </div>
            </StepCard>

            <OrderActions order={o} />

            <StepCard title="Items">
              <Items order={o} />
            </StepCard>

            {complaints.data && complaints.data.length > 0 && (
              <StepCard title="Your complaints">
                <ul className="flex flex-col gap-2">
                  {complaints.data.map((c) => (
                    <li key={c.id} className="flex items-start justify-between gap-3 text-sm">
                      <span className="text-text">{c.description}</span>
                      <StatusBadge code={c.status} map={complaintStatusMap} />
                    </li>
                  ))}
                </ul>
              </StepCard>
            )}
          </div>

          <div className="flex flex-col gap-3 lg:sticky lg:top-20">
            <BillBox
              title="Bill"
              rows={amountRows(amounts)}
              total={{
                label: o.final ? "Total" : "Estimated total",
                value: formatRupees(amounts.totalPaise),
              }}
              notes={
                <>
                  <p>Payment: {paymentMethodLabels[o.paymentMethod] ?? o.paymentMethod}</p>
                  {amounts.taxPaise > 0 && (
                    <p>Includes taxes of {formatRupees(amounts.taxPaise)}.</p>
                  )}
                  {o.final === null && (
                    <p>The final amount is confirmed when your order is packed.</p>
                  )}
                  {o.amountRefundedPaise > 0 && (
                    <p>Refunded: {formatRupees(o.amountRefundedPaise)}</p>
                  )}
                </>
              }
            />
            {o.amountDuePaise > 0 && (
              <p className="bg-teal-soft text-accent-text rounded-2xl p-3 text-sm font-bold">
                To pay on delivery: {formatRupees(o.amountDuePaise)}
              </p>
            )}
          </div>
        </div>
      </Container>
    </>
  );
}
