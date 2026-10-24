"use client";

import { formatDate } from "@fresh/core";
import { Button, Skeleton, toast } from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { listWishlist, removeFromWishlist } from "@/api/endpoints/account";
import { EmptyPage, LoadError } from "@/components/states";
import { describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

/**
 * Saved products. The API returns product ids and dates only, so each row is a dated placeholder until the wishlist
 * carries product details (docs/design/web.md §6, gap W5).
 */
export function WishlistView() {
  const queryClient = useQueryClient();
  const wishlist = useQuery({
    queryKey: queryKeys.wishlist,
    queryFn: () => listWishlist(browserHttp()),
  });
  const remove = useMutation({
    mutationFn: (productId: string) => removeFromWishlist(browserHttp(), productId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.wishlist }),
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });

  if (wishlist.isPending) return <Skeleton className="h-60 rounded-[22px]" />;
  if (wishlist.isError) {
    return (
      <LoadError
        title="We could not load your saved items"
        error={wishlist.error}
        onRetry={() => void wishlist.refetch()}
      />
    );
  }
  if (wishlist.data.length === 0) {
    return (
      <EmptyPage
        slot="EMPTY_WISHLIST"
        title="Nothing saved yet"
        description="Tap Save on a product to keep it here."
        actions={
          <Button asChild size="lg">
            <Link href="/shop">Browse the shop</Link>
          </Button>
        }
      />
    );
  }

  return (
    <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {wishlist.data.map((item) => (
        <li key={item.productId} className="flex flex-col gap-2">
          <div className="border-border bg-surface text-text-muted rounded-xl border p-4 text-sm">
            Saved on {formatDate(item.addedAt)}. Product details will appear here soon.
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="self-start"
            loading={remove.isPending && remove.variables === item.productId}
            onClick={() => remove.mutate(item.productId)}
          >
            Remove
          </Button>
        </li>
      ))}
    </ul>
  );
}
