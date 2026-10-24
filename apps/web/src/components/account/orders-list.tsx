"use client";

import { formatDayShort, formatRupees, formatTime, pageCount } from "@fresh/core";
import { AvatarCircle, Button, Pagination, Skeleton, StatusBadge, cn, focusRing } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { ChevronRight } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { listOrders } from "@/api/endpoints/orders";
import { EmptyPage, LoadError } from "@/components/states";
import { orderSlot } from "@/lib/order-progress";
import { queryKeys } from "@/lib/query-keys";
import { orderStatusMap } from "@/lib/status-maps";

const PAGE_SIZE = 10;

/** Orders as food-app cards (D95): the status character, number, status, slot, item count and the server's total. */
export function OrdersList() {
  const [page, setPage] = useState(1);
  const orders = useQuery({
    queryKey: queryKeys.orders(page),
    queryFn: () => listOrders(browserHttp(), { page, pageSize: PAGE_SIZE }),
    placeholderData: (previous) => previous,
  });

  if (orders.isPending) {
    return (
      <div className="flex flex-col gap-3">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-24 rounded-[22px]" />
        ))}
      </div>
    );
  }
  if (orders.isError) {
    return (
      <LoadError
        title="We could not load your orders"
        error={orders.error}
        onRetry={() => void orders.refetch()}
      />
    );
  }
  if (orders.data.items.length === 0) {
    return (
      <EmptyPage
        slot="EMPTY_ORDERS"
        title="No orders yet"
        description="Your orders will appear here, with tracking from the boat to your door."
        actions={
          <Button asChild size="lg">
            <Link href="/shop">Start shopping</Link>
          </Button>
        }
      />
    );
  }

  const pages = pageCount(orders.data);
  return (
    <div className="flex flex-col gap-4">
      <ul
        className="grid gap-3 lg:grid-cols-2"
        aria-busy={orders.isFetching}
        aria-label="Your orders"
      >
        {orders.data.items.map((o) => (
          <li key={o.id}>
            <Link
              href={`/orders/${encodeURIComponent(o.id)}`}
              className={cn(
                "border-border bg-surface-paper hover:border-border-strong flex items-center gap-4 rounded-[22px] border p-4 transition-transform hover:-translate-y-0.5 motion-reduce:transition-none motion-reduce:hover:translate-y-0",
                focusRing,
              )}
            >
              <AvatarCircle
                slot={orderSlot(o.status)}
                size="xs"
                className="size-16 border-2 sm:size-20"
              />
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-display text-text text-lg font-extrabold">
                    {o.orderNumber}
                  </span>
                  <StatusBadge code={o.status} map={orderStatusMap} />
                </div>
                <span className="text-text-muted text-sm">
                  {`Delivery ${formatDayShort(o.slotStartAt)}, ${formatTime(o.slotStartAt)}`}
                  {` · ${o.itemCount} ${o.itemCount === 1 ? "item" : "items"}`}
                </span>
              </div>
              <span className="font-display text-text text-lg font-extrabold tabular-nums">
                {formatRupees(o.finalTotalPaise ?? o.totalPaise)}
              </span>
              <ChevronRight aria-hidden="true" className="text-text-muted size-5 shrink-0" />
            </Link>
          </li>
        ))}
      </ul>
      {pages > 1 && (
        <Pagination
          page={page}
          pageSize={orders.data.pageSize}
          totalCount={orders.data.totalCount}
          onPageChange={setPage}
        />
      )}
    </div>
  );
}
