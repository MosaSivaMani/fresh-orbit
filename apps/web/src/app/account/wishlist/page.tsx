import type { Metadata } from "next";
import { WishlistView } from "@/components/account/wishlist-view";

export const metadata: Metadata = { title: "Saved items" };

export default function AccountWishlistPage() {
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-text text-3xl font-extrabold">Saved items</h1>
      <WishlistView />
    </div>
  );
}
