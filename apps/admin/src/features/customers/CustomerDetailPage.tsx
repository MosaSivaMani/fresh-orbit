import { formatDateTime, formatRupees, humanizeCode } from "@fresh/core";
import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  StatusBadge,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@fresh/ui";
import { ClipboardList, MapPin } from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useParams } from "react-router";
import { useCustomer, useCustomerOrders, useSetCustomerBlocked } from "../../api/customers";
import {
  type CustomerAddress,
  type CustomerDetail,
  type CustomerOrder,
} from "../../api/types/customers";
import { useCan } from "../../app/services";
import { Can, DetailList, PageHeader, QueryView, Section, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { CUSTOMER_STATUS, ORDER_STATUS } from "../../lib/status";

export function CustomerDetailPage() {
  const { customerId = "" } = useParams();
  const customer = useCustomer(customerId);
  return (
    <QueryView query={customer} what="customer">
      {(c) => <CustomerView customer={c} onConflict={() => customer.refetch()} />}
    </QueryView>
  );
}

function CustomerView({
  customer,
  onConflict,
}: {
  customer: CustomerDetail;
  onConflict: () => unknown;
}) {
  const can = useCan();
  const [confirming, setConfirming] = useState(false);
  const deleted = customer.status === "DELETED";
  const counts = customer.orderCounts;

  return (
    <>
      <PageHeader
        title={customer.fullName ?? "Unnamed customer"}
        description={customer.phone ?? customer.email ?? undefined}
        backTo="/customers"
        backLabel="Customers"
        meta={
          <>
            <StatusBadge code={customer.status} map={CUSTOMER_STATUS} />
            {customer.isBlocked && <Badge tone="danger">Blocked</Badge>}
          </>
        }
        actions={
          !deleted && (
            <Can perm="customers.manage">
              <Button
                variant={customer.isBlocked ? "outline" : "danger"}
                size="sm"
                onClick={() => setConfirming(true)}
              >
                {customer.isBlocked ? "Unblock" : "Block"}
              </Button>
            </Can>
          )
        }
      />
      <Tabs defaultValue="profile" className="flex flex-col gap-4">
        <TabsList>
          <TabsTrigger value="profile">Profile</TabsTrigger>
          <TabsTrigger value="addresses">Addresses ({customer.addresses.length})</TabsTrigger>
          {can("orders.view") && <TabsTrigger value="orders">Orders</TabsTrigger>}
        </TabsList>
        <TabsContent value="profile">
          <Section title="Profile">
            <DetailList
              items={[
                { label: "Name", value: customer.fullName },
                { label: "Phone", value: customer.phone },
                { label: "Email", value: customer.email },
                {
                  label: "Marketing messages",
                  value: customer.marketingOptIn ? "Opted in" : "Not opted in",
                },
                ...(customer.deletedAt
                  ? [{ label: "Deleted at", value: formatDateTime(customer.deletedAt) }]
                  : []),
              ]}
            />
          </Section>
          <Section title="Orders">
            <DetailList
              items={[
                { label: "Placed", value: String(counts.total) },
                { label: "Open", value: String(counts.open) },
                { label: "Delivered", value: String(counts.delivered) },
                { label: "Cancelled", value: String(counts.cancelled) },
                {
                  label: "Last order",
                  value: counts.lastPlacedAt ? formatDateTime(counts.lastPlacedAt) : "—",
                },
              ]}
            />
          </Section>
        </TabsContent>
        <TabsContent value="addresses">
          <Addresses addresses={customer.addresses} />
        </TabsContent>
        {can("orders.view") && (
          <TabsContent value="orders">
            <CustomerOrders customerId={customer.customerId} />
          </TabsContent>
        )}
      </Tabs>
      <BlockDialog
        customer={customer}
        open={confirming}
        onOpenChange={setConfirming}
        onConflict={onConflict}
      />
    </>
  );
}

function Addresses({ addresses }: { addresses: CustomerAddress[] }) {
  if (addresses.length === 0) {
    return <EmptyState icon={<MapPin aria-hidden="true" />} title="No saved addresses" />;
  }
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      {addresses.map((a) => (
        <Section
          key={a.addressId}
          title={a.label}
          actions={
            <>
              {a.isDefault && <Badge tone="accent">Default</Badge>}
              {a.isServiceable === false && <Badge tone="warning">Not serviceable</Badge>}
            </>
          }
        >
          <address className="flex flex-col gap-0.5 text-sm not-italic">
            <span className="font-medium">{a.recipientName}</span>
            <span>{a.line1}</span>
            {a.line2 && <span>{a.line2}</span>}
            {a.landmark && <span>Near {a.landmark}</span>}
            <span>
              {a.locality}, {a.cityName} {a.pincode}
            </span>
            <span className="text-text-muted">{a.phone}</span>
          </address>
        </Section>
      ))}
    </div>
  );
}

function CustomerOrders({ customerId }: { customerId: string }) {
  const [paging, setPaging] = useState({ page: 1, pageSize: 20 });
  const orders = useCustomerOrders(customerId, paging);

  const columns = useMemo<ColumnDef<CustomerOrder>[]>(
    () => [
      {
        id: "orderNumber",
        header: "Order",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/orders/${row.original.orderId}`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.orderNumber}
          </Link>
        ),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => <StatusBadge code={row.original.status} map={ORDER_STATUS} />,
      },
      {
        id: "placedAt",
        header: "Placed",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.placedAt),
      },
      {
        id: "paymentMethod",
        header: "Payment",
        enableSorting: false,
        cell: ({ row }) => humanizeCode(row.original.paymentMethod),
      },
      {
        id: "total",
        header: "Total",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.finalTotalPaise ?? row.original.totalPaise),
      },
    ],
    [],
  );

  return (
    <DataTable
      caption="Customer orders"
      columns={columns}
      data={orders.data?.items ?? []}
      getRowId={(o) => o.orderId}
      getRowLabel={(o) => o.orderNumber}
      page={paging.page}
      pageSize={paging.pageSize}
      totalCount={orders.data?.totalCount ?? 0}
      onPageChange={(page) => setPaging((p) => ({ ...p, page }))}
      onPageSizeChange={(pageSize) => setPaging({ page: 1, pageSize })}
      isLoading={orders.isFetching}
      error={tableError(orders.error)}
      onRetry={() => void orders.refetch()}
      emptyState={<EmptyState icon={<ClipboardList aria-hidden="true" />} title="No orders yet" />}
    />
  );
}

function BlockDialog({
  customer,
  open,
  onOpenChange,
  onConflict,
}: {
  customer: CustomerDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const setBlocked = useSetCustomerBlocked(customer.customerId);
  const block = !customer.isBlocked;
  const name = customer.fullName ?? "this customer";
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title={block ? `Block ${name}?` : `Unblock ${name}?`}
        description={
          block
            ? "They are signed out everywhere and cannot sign in or place orders until unblocked."
            : "They can sign in and place orders again."
        }
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Keep as is
            </Button>
            <Button
              variant={block ? "danger" : "primary"}
              loading={setBlocked.isPending}
              onClick={() =>
                setBlocked.mutate(
                  { blocked: block, version: customer.version },
                  {
                    onSuccess: () => {
                      notifySaved(block ? "Customer blocked" : "Customer unblocked");
                      onOpenChange(false);
                    },
                    onError: (error) => {
                      onOpenChange(false);
                      reportError(error, { onConflict });
                    },
                  },
                )
              }
            >
              {block ? "Block customer" : "Unblock customer"}
            </Button>
          </>
        }
      />
    </Dialog>
  );
}
