import {
  formatDateTime,
  formatQuantity,
  formatRupees,
  formatTime,
  humanizeCode,
} from "@fresh/core";
import { Badge, Button, StatusBadge, Tabs, TabsContent, TabsList, TabsTrigger } from "@fresh/ui";
import { Ban, Gift, PlayCircle, Replace } from "lucide-react";
import { useState } from "react";
import { useParams } from "react-router";
import { useOrder, useStartPreparing } from "../../api/orders";
import {
  type AdminOrderDetail,
  type OrderItem,
  type OrderTotals,
  type PackResponse,
} from "../../api/types/orders";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { ORDER_ITEM_STATUS, ORDER_STATUS, SUBSTITUTION_STATUS } from "../../lib/status";
import { OrderPaymentsPanel } from "../payments/OrderPaymentsPanel";
import {
  CancelOrderDialog,
  GoodwillDialog,
  ProposeSubstitutionDialog,
  RecordSubstitutionAnswer,
} from "./OrderDialogs";
import { PackForm, PackResult } from "./PackForm";

type Order = AdminOrderDetail & { version: string };

const STAFF_CANCELLABLE = new Set(["CONFIRMED", "PREPARING", "PACKED"]);
const GOODWILL_ALLOWED = new Set(["PACKED", "OUT_FOR_DELIVERY", "DELIVERED"]);

export function OrderDetailPage() {
  const { orderId = "" } = useParams();
  const query = useOrder(orderId);
  return (
    <QueryView query={query} what="order">
      {(order) => <OrderDetail order={order} reload={() => query.refetch()} />}
    </QueryView>
  );
}

function OrderDetail({ order, reload }: { order: Order; reload: () => unknown }) {
  const can = useCan();
  const { nameOf } = useLocationScope();
  const start = useStartPreparing(order.id);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [goodwillOpen, setGoodwillOpen] = useState(false);
  const [substituteItem, setSubstituteItem] = useState<OrderItem | null>(null);
  const [packResult, setPackResult] = useState<PackResponse | null>(null);
  const [tab, setTab] = useState(
    order.status === "PREPARING" && can("orders.pack") ? "pack" : "items",
  );

  const packing = order.status === "PREPARING" && can("orders.pack");
  const canPropose =
    order.status === "PREPARING" && can("orders.pack") && order.substitutionPreference !== "NONE";

  return (
    <>
      <PageHeader
        backTo="/orders"
        backLabel="Orders"
        title={order.orderNumber}
        meta={<StatusBadge code={order.status} map={ORDER_STATUS} />}
        description={`Placed ${formatDateTime(order.placedAt)} · slot ${formatTime(order.slot.startAt)} – ${formatTime(order.slot.endAt)}${
          order.fulfillmentLocationId ? ` · ${nameOf(order.fulfillmentLocationId)}` : ""
        }`}
        actions={
          <>
            {order.status === "CONFIRMED" && can("orders.pack") && (
              <Button
                leadingIcon={<PlayCircle aria-hidden="true" />}
                loading={start.isPending}
                onClick={() =>
                  start.mutate(order.version, {
                    onSuccess: () => {
                      notifySaved("Preparing started");
                      setTab("pack");
                    },
                    onError: (error) => reportError(error, { onConflict: reload }),
                  })
                }
              >
                Start preparing
              </Button>
            )}
            {GOODWILL_ALLOWED.has(order.status) && can("refunds.request") && (
              <Button
                variant="outline"
                leadingIcon={<Gift aria-hidden="true" />}
                onClick={() => setGoodwillOpen(true)}
              >
                Goodwill
              </Button>
            )}
            {STAFF_CANCELLABLE.has(order.status) && can("orders.cancel") && (
              <Button
                variant="outline"
                leadingIcon={<Ban aria-hidden="true" />}
                onClick={() => setCancelOpen(true)}
              >
                Cancel order
              </Button>
            )}
          </>
        }
      />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_20rem]">
        <div className="min-w-0">
          <Tabs value={tab} onValueChange={setTab}>
            <TabsList>
              {packing && <TabsTrigger value="pack">Weigh and pack</TabsTrigger>}
              <TabsTrigger value="items">Items</TabsTrigger>
              <TabsTrigger value="timeline">Timeline</TabsTrigger>
              <TabsTrigger value="payment">Payment</TabsTrigger>
              <TabsTrigger value="notes">Notes</TabsTrigger>
            </TabsList>

            {packing && (
              <TabsContent value="pack">
                <PackForm
                  order={order}
                  onConflict={reload}
                  onPacked={(result) => {
                    setPackResult(result);
                    setTab("items");
                    notifySaved(`${order.orderNumber} packed`);
                  }}
                />
              </TabsContent>
            )}

            <TabsContent value="items" className="flex flex-col gap-4">
              {packResult && <PackResult result={packResult} order={order} />}
              <ItemsTable order={order} onPropose={canPropose ? setSubstituteItem : undefined} />
              {order.substitutions.length > 0 && (
                <Section title="Substitutions">
                  <ul className="flex flex-col gap-3 text-sm">
                    {order.substitutions.map((s) => {
                      const item = order.items.find((i) => i.id === s.orderItemId);
                      return (
                        <li key={s.id} className="flex flex-wrap items-center gap-3">
                          <StatusBadge code={s.status} map={SUBSTITUTION_STATUS} />
                          <span>
                            Line {item?.lineNo ?? "?"}: {s.proposedName},{" "}
                            {formatQuantity(s.proposedQuantity)}
                          </span>
                          <span className="text-text-muted">
                            difference {formatRupees(s.priceDifferencePaise)}
                            {s.expiresAt && s.status === "PROPOSED"
                              ? ` · expires ${formatTime(s.expiresAt)}`
                              : ""}
                          </span>
                          {s.status === "PROPOSED" && can("orders.pack") && (
                            <RecordSubstitutionAnswer
                              order={order}
                              substitution={s}
                              onConflict={reload}
                            />
                          )}
                        </li>
                      );
                    })}
                  </ul>
                </Section>
              )}
            </TabsContent>

            <TabsContent value="timeline">
              <Timeline order={order} />
            </TabsContent>

            <TabsContent value="payment" className="flex flex-col gap-4">
              <Section title="Payment">
                <DetailList
                  columns={3}
                  items={[
                    { label: "Method", value: humanizeCode(order.paymentMethod) },
                    {
                      label: "Status",
                      value: order.payment ? humanizeCode(order.payment.status) : "—",
                    },
                    { label: "Amount due", value: formatRupees(order.amountDuePaise) },
                    { label: "Paid", value: formatRupees(order.amountPaidPaise) },
                    { label: "Refunded", value: formatRupees(order.amountRefundedPaise) },
                    {
                      label: "Invoice",
                      value: order.invoice?.invoiceNumber ?? "Issued at delivery",
                    },
                  ]}
                />
              </Section>
              <OrderPaymentsPanel orderId={order.id} />
            </TabsContent>

            <TabsContent value="notes">
              <Section title="Customer notes">
                <p className="text-sm whitespace-pre-wrap">
                  {order.customerNotes || "No notes from the customer."}
                </p>
              </Section>
            </TabsContent>
          </Tabs>
        </div>

        <aside className="flex flex-col gap-4" aria-label="Order summary">
          <Section title="Customer">
            <DetailList
              columns={1}
              items={[
                { label: "Recipient", value: order.address.recipientName },
                {
                  label: "Phone",
                  value: (
                    <a
                      href={`tel:${order.address.phone}`}
                      className="text-accent-text hover:underline"
                    >
                      {order.address.phone}
                    </a>
                  ),
                },
                {
                  label: "Address",
                  value: [
                    order.address.line1,
                    order.address.line2,
                    order.address.landmark,
                    order.address.locality,
                    `${order.address.cityName} ${order.address.pincode}`,
                  ]
                    .filter(Boolean)
                    .join(", "),
                },
                { label: "Substitutions", value: humanizeCode(order.substitutionPreference) },
              ]}
            />
          </Section>
          <Section title="Amounts">
            <Totals title="Estimate" totals={order.estimate} />
            {order.final ? (
              <Totals title="Final (after packing)" totals={order.final} />
            ) : (
              <p className="text-text-muted text-xs">
                Final amounts are set when the order is packed.
              </p>
            )}
            {order.couponCode && (
              <p className="text-sm">
                Coupon <Badge tone="highlight">{order.couponCode}</Badge>
              </p>
            )}
          </Section>
        </aside>
      </div>

      <CancelOrderDialog
        order={order}
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        onConflict={reload}
      />
      {goodwillOpen && (
        <GoodwillDialog
          order={order}
          open={goodwillOpen}
          onOpenChange={setGoodwillOpen}
          onConflict={reload}
        />
      )}
      {substituteItem && (
        <ProposeSubstitutionDialog
          order={order}
          item={substituteItem}
          open
          onOpenChange={(open) => !open && setSubstituteItem(null)}
          onConflict={reload}
        />
      )}
    </>
  );
}

function ItemsTable({
  order,
  onPropose,
}: {
  order: Order;
  onPropose: ((item: OrderItem) => void) | undefined;
}) {
  return (
    <div className="border-border bg-surface overflow-x-auto rounded-lg border">
      <table className="w-full text-sm">
        <caption className="sr-only">Order lines</caption>
        <thead className="bg-muted/60 text-text-muted text-left text-xs uppercase">
          <tr>
            <th scope="col" className="px-3 py-2">
              #
            </th>
            <th scope="col" className="px-3 py-2">
              Item
            </th>
            <th scope="col" className="px-3 py-2 text-right">
              Ordered
            </th>
            <th scope="col" className="px-3 py-2 text-right">
              Reserved (gross)
            </th>
            <th scope="col" className="px-3 py-2 text-right">
              Packed
            </th>
            <th scope="col" className="px-3 py-2 text-right">
              Charged
            </th>
            <th scope="col" className="px-3 py-2 text-right">
              Total
            </th>
            <th scope="col" className="px-3 py-2">
              Status
            </th>
            {onPropose && (
              <th scope="col" className="px-3 py-2">
                <span className="sr-only">Actions</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => {
            const hasPending = order.substitutions.some(
              (s) => s.orderItemId === item.id && s.status === "PROPOSED",
            );
            return (
              <tr key={item.id} className="border-border border-t">
                <td className="px-3 py-2 tabular-nums">{item.lineNo}</td>
                <td className="px-3 py-2">
                  <span className="block font-medium">{item.productName}</span>
                  <span className="text-text-muted block text-xs">
                    {item.variantName} · {item.preparationName} ·{" "}
                    {formatRupees(item.unitPricePaise)}/
                    {item.orderedQuantity.unit === "GRAM" ? "kg" : "unit"}
                  </span>
                  {item.batches && item.batches.length > 0 && (
                    <span className="text-text-muted block text-xs">
                      Batches:{" "}
                      {item.batches
                        .map((b) => `${b.batchCode} (${formatQuantity(b.quantity)})`)
                        .join(", ")}
                    </span>
                  )}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {formatQuantity(item.orderedQuantity)}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {item.grossReservedQuantity ? formatQuantity(item.grossReservedQuantity) : "—"}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {item.packedQuantity ? formatQuantity(item.packedQuantity) : "—"}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {item.chargedQuantity ? formatQuantity(item.chargedQuantity) : "—"}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {formatRupees(item.finalTotalPaise ?? item.estimatedTotalPaise)}
                  {item.finalTotalPaise == null && (
                    <span className="text-text-muted block text-xs">estimate</span>
                  )}
                </td>
                <td className="px-3 py-2">
                  <StatusBadge code={item.status} map={ORDER_ITEM_STATUS} />
                </td>
                {onPropose && (
                  <td className="px-3 py-2">
                    {item.status === "ACTIVE" && !hasPending && (
                      <Button
                        size="sm"
                        variant="ghost"
                        leadingIcon={<Replace aria-hidden="true" />}
                        onClick={() => onPropose(item)}
                      >
                        Substitute
                      </Button>
                    )}
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function Totals({ title, totals }: { title: string; totals: OrderTotals }) {
  const rows: [string, number][] = [
    ["Items", totals.itemsPaise],
    ["Preparation", totals.preparationPaise],
    ["Discount", totals.discountPaise],
    ["Delivery fee", totals.deliveryFeePaise],
    ["Tax included", totals.taxPaise],
  ];
  return (
    <div className="flex flex-col gap-1">
      <h3 className="text-text-muted text-xs font-semibold tracking-wide uppercase">{title}</h3>
      <dl className="flex flex-col gap-0.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-3">
            <dt className="text-text-muted">{label}</dt>
            <dd className="tabular-nums">{formatRupees(value)}</dd>
          </div>
        ))}
        <div className="border-border flex justify-between gap-3 border-t pt-1 font-semibold">
          <dt>Total</dt>
          <dd className="tabular-nums">{formatRupees(totals.totalPaise)}</dd>
        </div>
      </dl>
    </div>
  );
}

function Timeline({ order }: { order: Order }) {
  const history = order.history ?? [];
  if (history.length === 0) {
    return <p className="text-text-muted text-sm">No status history returned for this order.</p>;
  }
  return (
    <ol className="border-border flex flex-col gap-4 border-l pl-4">
      {history.map((h, index) => (
        <li key={`${h.toStatus}-${h.changedAt}-${index}`} className="flex flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <StatusBadge code={h.toStatus} map={ORDER_STATUS} />
            <span className="text-text-muted text-xs">{formatDateTime(h.changedAt)}</span>
          </span>
          <span className="text-text-muted text-xs">
            {humanizeCode(h.actorType)}
            {h.actorName ? ` · ${h.actorName}` : ""}
            {h.reason ? ` · ${humanizeCode(h.reason)}` : ""}
          </span>
        </li>
      ))}
      {(order.adjustments ?? []).map((a) => (
        <li key={a.id} className="flex flex-col gap-1">
          <span className="flex items-center gap-2 text-sm">
            <Badge tone="info">{humanizeCode(a.kind)}</Badge>
            <span className="tabular-nums">{formatRupees(a.amountPaise)}</span>
            <span className="text-text-muted text-xs">{formatDateTime(a.createdAt)}</span>
          </span>
          {a.reason && <span className="text-text-muted text-xs">{a.reason}</span>}
        </li>
      ))}
    </ol>
  );
}
