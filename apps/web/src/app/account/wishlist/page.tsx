import type { Metadata } from "next";
import { WishlistView } from "@/components/account/wishlist-view";

export const metadata: Metadata = { title: "Saved items" };

export default function AccountWishlistPage() {
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-text text-2xl font-bold">Saved items</h1>
      <WishlistView />
    </div>
  );
}
