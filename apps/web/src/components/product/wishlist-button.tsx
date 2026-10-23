"use client";

import { Button, toast } from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Heart } from "lucide-react";
import { useRouter } from "next/navigation";
import { browserHttp } from "@/api/browser";
import { addToWishlist, listWishlist, removeFromWishlist } from "@/api/endpoints/account";
import { useSession } from "@/auth/use-session";
import { describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

/** Save for later. Signed-out visitors are sent to sign in and brought back. */
export function WishlistButton({ productId, slug }: { productId: string; slug: string }) {
  const { status } = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const wishlist = useQuery({
    queryKey: queryKeys.wishlist,
    queryFn: () => listWishlist(browserHttp()),
    enabled: status === "signedIn",
  });
  const saved = wishlist.data?.some((item) => item.productId === productId) ?? false;
  const toggle = useMutation({
    mutationFn: () =>
      saved
        ? removeFromWishlist(browserHttp(), productId)
        : addToWishlist(browserHttp(), productId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.wishlist }),
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });

  return (
    <Button
      variant="outline"
      size="md"
      aria-pressed={saved}
      loading={toggle.isPending}
      leadingIcon={<Heart aria-hidden="true" className={saved ? "fill-current" : undefined} />}
      onClick={() => {
        if (status !== "signedIn") {
          router.push(`/login?next=${encodeURIComponent(`/products/${slug}`)}`);
          return;
        }
        toggle.mutate();
      }}
    >
      {saved ? "Saved" : "Save"}
    </Button>
  );
}
