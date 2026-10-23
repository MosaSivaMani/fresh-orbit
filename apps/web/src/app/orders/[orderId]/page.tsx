import type { Metadata } from "next";
import { RequireAuth } from "@/components/auth/require-auth";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { OrderDetailView } from "@/components/orders/order-detail-view";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Your order", robots: { index: false } };

export default async function OrderPage(props: PageProps<"/orders/[orderId]">) {
  const { orderId } = await props.params;
  return (
    <Container className="flex flex-col gap-6 py-8">
      <Breadcrumbs
        items={[
          { href: "/account", label: "Account" },
          { href: "/account/orders", label: "Orders" },
          { label: "Order" },
        ]}
      />
      <RequireAuth>
        <OrderDetailView orderId={orderId} />
      </RequireAuth>
    </Container>
  );
}
