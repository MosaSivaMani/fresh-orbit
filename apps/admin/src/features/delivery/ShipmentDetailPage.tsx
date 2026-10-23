import { formatDateTime, formatRupees, humanizeCode } from "@fresh/core";
import { StatusBadge } from "@fresh/ui";
import { Link, useParams } from "react-router";
import { useRiders, useShipment } from "../../api/delivery";
import { useLocationScope } from "../../app/location-scope";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { SHIPMENT_STATUS } from "../../lib/status";
import { ShipmentActions } from "./ShipmentActions";
import { shortId } from "./shared";

const CASH_MODE_LABELS: Readonly<Record<string, string>> = { CASH: "Cash", UPI: "UPI" };

/** One shipment: timeline, cold chain, cash collected and attempts. The delivery code is never shown to staff. */
export function ShipmentDetailPage() {
  const { shipmentId = "" } = useParams();
  const shipment = useShipment(shipmentId);
  const scope = useLocationScope();
  const locationId = shipment.data?.fulfillmentLocationId;
  const riders = useRiders(
    { fulfillmentLocationId: locationId ?? "", pageSize: 100 },
    Boolean(locationId),
  );
  const riderName = (id: string | null) =>
    id
      ? (riders.data?.items.find((r) => r.id === id)?.fullName ?? `Rider ${shortId(id)}`)
      : "Not assigned";

  return (
    <QueryView query={shipment} what="shipment">
      {(s) => (
        <>
          <PageHeader
            backTo="/delivery"
            backLabel="Shipments"
            title={`Shipment ${shortId(s.id)}`}
            meta={<StatusBadge code={s.status} map={SHIPMENT_STATUS} />}
            description={scope.nameOf(s.fulfillmentLocationId)}
            actions={<ShipmentActions shipment={s} size="md" />}
          />
          <div className="flex flex-col gap-6">
            <Section title="Delivery">
              <DetailList
                columns={3}
                items={[
                  {
                    label: "Order",
                    value: (
                      <Link
                        to={`/orders/${s.orderId}`}
                        className="text-accent-text hover:underline"
                      >
                        Order {shortId(s.orderId)}
                      </Link>
                    ),
                  },
                  { label: "Rider", value: riderName(s.riderId) },
                  { label: "Placed", value: formatDateTime(s.placedAt) },
                  { label: "Packed", value: s.packedAt ? formatDateTime(s.packedAt) : "—" },
                  {
                    label: "Dispatched",
                    value: s.dispatchedAt ? formatDateTime(s.dispatchedAt) : "—",
                  },
                  {
                    label: "Delivered",
                    value: s.deliveredAt ? formatDateTime(s.deliveredAt) : "—",
                  },
                  ...(s.returnedAt
                    ? [{ label: "Returned", value: formatDateTime(s.returnedAt) }]
                    : []),
                  ...(s.cancelledAt
                    ? [{ label: "Cancelled", value: formatDateTime(s.cancelledAt) }]
                    : []),
                ]}
              />
            </Section>
            <Section title="Cold chain and delivery code">
              <DetailList
                columns={3}
                items={[
                  { label: "Cold box used", value: s.coldBoxUsed ? "Yes" : "No" },
                  {
                    label: "Dispatch temperature",
                    value:
                      s.dispatchTemperatureC == null ? (
                        "—"
                      ) : (
                        <span
                          className={
                            s.temperatureWarning ? "text-warning-text font-medium" : undefined
                          }
                        >
                          {s.dispatchTemperatureC} °C{s.temperatureWarning ? " (above limit)" : ""}
                        </span>
                      ),
                  },
                  {
                    label: "Wrong code entries",
                    value: s.deliveryCodeLocked
                      ? `${s.deliveryCodeFailures} (locked)`
                      : String(s.deliveryCodeFailures),
                  },
                ]}
              />
            </Section>
            <Section title="Cash or UPI collected">
              {s.cashCollectedPaise == null ? (
                <p className="text-text-muted text-sm">Nothing collected at the door yet.</p>
              ) : (
                <DetailList
                  columns={3}
                  items={[
                    { label: "Amount", value: formatRupees(s.cashCollectedPaise) },
                    {
                      label: "Mode",
                      value: s.cashMode ? (CASH_MODE_LABELS[s.cashMode] ?? s.cashMode) : "—",
                    },
                    { label: "UPI reference", value: s.upiReference ?? "—" },
                    {
                      label: "Collected",
                      value: s.cashCollectedAt ? formatDateTime(s.cashCollectedAt) : "—",
                    },
                  ]}
                />
              )}
            </Section>
            <Section title="Attempts">
              {s.attempts.length === 0 ? (
                <p className="text-text-muted text-sm">No delivery attempts yet.</p>
              ) : (
                <ol className="divide-border flex flex-col divide-y">
                  {s.attempts.map((a) => (
                    <li key={a.attemptNo} className="flex flex-col gap-0.5 py-2 text-sm">
                      <span className="font-medium">
                        Attempt {a.attemptNo}: {humanizeCode(a.outcome)}
                      </span>
                      <span className="text-text-muted">{formatDateTime(a.attemptedAt)}</span>
                      {a.notes && <span>{a.notes}</span>}
                    </li>
                  ))}
                </ol>
              )}
            </Section>
          </div>
        </>
      )}
    </QueryView>
  );
}
