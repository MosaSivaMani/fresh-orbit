import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { OrderSuccess } from "@/components/orders/order-success";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Order placed", robots: { index: false } };

export default async function OrderSuccessPage(props: PageProps<"/orders/[orderId]/success">) {
  const { orderId } = await props.params;
  return (
    <Container className="py-12">
      <RequireAuth>
        <OrderSuccess orderId={orderId} />
      </RequireAuth>
    </Container>
  );
}
