import type { Metadata } from "next";
import { OrdersList } from "@/components/account/orders-list";

export const metadata: Metadata = { title: "Orders" };

export default function AccountOrdersPage() {
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-text text-2xl font-bold">Orders</h1>
      <OrdersList />
    </div>
  );
}
