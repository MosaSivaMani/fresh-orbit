import type { Metadata } from "next";
import { OrdersList } from "@/components/account/orders-list";

export const metadata: Metadata = { title: "Orders" };

export default function AccountOrdersPage() {
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-text text-3xl font-extrabold">Orders</h1>
      <OrdersList />
    </div>
  );
}
