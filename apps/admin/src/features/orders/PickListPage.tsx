import { formatQuantity } from "@fresh/core";
import { Button, EmptyState } from "@fresh/ui";
import { ClipboardList, Printer } from "lucide-react";
import { useSearchParams } from "react-router";
import { usePickList } from "../../api/orders";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, QueryView } from "../../components/page";
import { AllCaughtUp } from "../../components/states";

/**
 * Pick list for one slot at one location (ORD-R60): gross quantities per variant and preparation, summed by the
 * server from the reservations. Reached from the orders list (slot link or bulk action).
 */
export function PickListPage() {
  const [params] = useSearchParams();
  const { nameOf } = useLocationScope();
  const slotId = params.get("slotId");
  const fulfillmentLocationId = params.get("fulfillmentLocationId");
  const query = usePickList(
    slotId && fulfillmentLocationId ? { slotId, fulfillmentLocationId } : null,
  );

  if (!slotId || !fulfillmentLocationId) {
    return (
      <>
        <PageHeader title="Pick list" backTo="/orders" backLabel="Orders" />
        <EmptyState
          icon={<ClipboardList aria-hidden="true" />}
          title="Choose a slot first"
          description="Open the pick list from a slot in the orders list."
        />
      </>
    );
  }

  return (
    <QueryView query={query} what="pick list">
      {(pick) => (
        <>
          <PageHeader
            title="Pick list"
            backTo="/orders"
            backLabel="Orders"
            description={nameOf(fulfillmentLocationId)}
            actions={
              <Button
                variant="outline"
                leadingIcon={<Printer aria-hidden="true" />}
                onClick={() => window.print()}
              >
                Print
              </Button>
            }
          />
          {pick.lines.length === 0 ? (
            <AllCaughtUp title="Nothing to pick for this slot" />
          ) : (
            <div className="border-border bg-surface overflow-x-auto rounded-lg border">
              <table className="w-full text-sm">
                <caption className="sr-only">Gross quantities to pick</caption>
                <thead className="bg-muted/60 text-text-muted text-left text-xs uppercase">
                  <tr>
                    <th scope="col" className="px-3 py-2">
                      Item
                    </th>
                    <th scope="col" className="px-3 py-2">
                      Preparation
                    </th>
                    <th scope="col" className="px-3 py-2 text-right">
                      Gross to pick
                    </th>
                    <th scope="col" className="px-3 py-2 text-right">
                      Orders
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {pick.lines.map((item) => (
                    <tr
                      key={`${item.variantId}-${item.preparationCode}`}
                      className="border-border border-t"
                    >
                      <td className="px-3 py-2">
                        <span className="font-medium">{item.productName}</span>{" "}
                        <span className="text-text-muted">{item.variantName}</span>
                      </td>
                      <td className="px-3 py-2">{item.preparationName ?? "—"}</td>
                      <td className="px-3 py-2 text-right text-base font-semibold tabular-nums">
                        {formatQuantity({ value: item.grossQuantity, unit: item.stockUnit })}
                      </td>
                      <td className="px-3 py-2 text-right tabular-nums">{item.orderCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </QueryView>
  );
}
