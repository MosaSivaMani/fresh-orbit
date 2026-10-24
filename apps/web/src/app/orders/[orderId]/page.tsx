import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { OrderDetailView } from "@/components/orders/order-detail-view";

export const metadata: Metadata = { title: "Your order", robots: { index: false } };

export default async function OrderPage(props: PageProps<"/orders/[orderId]">) {
  const { orderId } = await props.params;
  return (
    <RequireAuth>
      <OrderDetailView orderId={orderId} />
    </RequireAuth>
  );
}
