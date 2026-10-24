"use client";

import { formatDayShort, formatRupees, formatTime, pageCount } from "@fresh/core";
import { Button, EmptyState, ErrorState, Pagination, Skeleton, StatusBadge } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { ChevronRight, Package } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import { listOrders } from "@/api/endpoints/orders";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { orderStatusMap } from "@/lib/status-maps";

const PAGE_SIZE = 10;

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
          <Skeleton key={i} className="h-20 rounded-xl" />
        ))}
      </div>
    );
  }
  if (orders.isError) {
    return (
      <ErrorState
        title="We could not load your orders"
        description={describeError(orders.error)}
        traceId={asApiError(orders.error)?.traceId}
        onRetry={() => void orders.refetch()}
      />
    );
  }
  if (orders.data.items.length === 0) {
    return (
      <EmptyState
        icon={<Package aria-hidden="true" />}
        title="No orders yet"
        description="Your orders will appear here."
        action={
          <Button asChild>
            <Link href="/shop">Start shopping</Link>
          </Button>
        }
      />
    );
  }

  const pages = pageCount(orders.data);
  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col gap-3" aria-busy={orders.isFetching}>
        {orders.data.items.map((o) => (
          <li key={o.id}>
            <Link
              href={`/orders/${encodeURIComponent(o.id)}`}
              className="border-border bg-surface hover:border-border-strong flex items-center gap-4 rounded-xl border p-4"
            >
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-text font-semibold">{o.orderNumber}</span>
                  <StatusBadge code={o.status} map={orderStatusMap} />
                </div>
                <span className="text-text-muted text-sm">
                  {`Delivery ${formatDayShort(o.slotStartAt)}, ${formatTime(o.slotStartAt)}`}
                  {` · ${o.itemCount} ${o.itemCount === 1 ? "item" : "items"}`}
                </span>
              </div>
              <span className="text-text font-semibold tabular-nums">
                {formatRupees(o.finalTotalPaise ?? o.totalPaise)}
              </span>
              <ChevronRight aria-hidden="true" className="text-text-muted size-5" />
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
