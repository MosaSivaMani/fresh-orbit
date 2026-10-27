"use client";

import { formatDate, formatRupees } from "@fresh/core";
import { Button, CompactProductCard, Skeleton, toast } from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { listWishlist, removeFromWishlist } from "@/api/endpoints/account";
import { type WishlistItem } from "@/api/types/account";
import { ProductImage } from "@/components/catalog/product-card";
import { EmptyPage, LoadError } from "@/components/states";
import { describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

/**
 * One saved product on the shared `CompactProductCard` (D207): picture, name, the server's price for the visitor's
 * pincode, and a link to the product.
 */
function SavedCard({ item }: { item: WishlistItem }) {
  const product = item.product;
  if (!product) {
    // The product is no longer listed; the row stays removable.
    return (
      <div className="bg-surface-paper border-border text-text-muted flex min-h-28 flex-col justify-center gap-1 rounded-[20px] border p-4 text-sm">
        <p className="text-text font-semibold">No longer available</p>
        <p>Saved on {formatDate(item.addedAt)}.</p>
      </div>
    );
  }
  return (
    <CompactProductCard
      href={`/products/${product.slug}`}
      linkAs={Link}
      picture={<ProductImage src={product.imageUrl} alt="" sizes="96px" className="size-full" />}
      name={product.name}
      price={
        product.price ? (
          <>
            <span className="font-bold tabular-nums">
              {formatRupees(product.price.unitPricePaise)}
            </span>{" "}
            / {product.price.perUnit}
            {product.price.compareAtPaise != null && (
              <span className="text-text-muted ml-2 tabular-nums line-through">
                {formatRupees(product.price.compareAtPaise)}
              </span>
            )}
          </>
        ) : (
          <span className="text-text-muted">Set your pincode to see the price</span>
        )
      }
      note={product.isAvailable ? undefined : "Sold out for now"}
    />
  );
}

/** Saved products as cards from the server's wishlist (product summary per item, gap W5 closed). */
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
          <SavedCard item={item} />
          <Button
            variant="ghost"
            size="sm"
            className="self-start"
            loading={remove.isPending && remove.variables === item.productId}
            onClick={() => remove.mutate(item.productId)}
            aria-label={`Remove ${item.product?.name ?? "saved item"}`}
          >
            Remove
          </Button>
        </li>
      ))}
    </ul>
  );
}
