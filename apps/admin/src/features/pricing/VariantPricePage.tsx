import { formatRupees } from "@fresh/core";
import {
  Badge,
  Button,
  Dialog,
  DialogContent,
  EmptyState,
  FormField,
  Input,
  Select,
  Skeleton,
} from "@fresh/ui";
import { useState } from "react";
import { useParams } from "react-router";
import { usePreparationMethods, useProduct } from "../../api/catalog";
import { useChargeSchedule, useEndPrice, usePriceSchedule, usePrices } from "../../api/pricing";
import { type Id } from "../../api/types/common";
import { type ChargePeriod, type PricePeriod } from "../../api/types/pricing";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { LocationRequired, PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { SetChargeDialog } from "./SetChargeDialog";
import { SetPriceDialog } from "./SetPriceDialog";
import { CHARGE_BASIS, PRICE_UNIT, periodText } from "./shared";

export function VariantPricePage() {
  const { variantId = "" } = useParams();
  const scope = useLocationScope();
  if (!scope.locationId) {
    return (
      <>
        <PageHeader title="Variant price" backTo="/pricing" backLabel="Prices" />
        <LocationRequired action="see this variant's prices" />
      </>
    );
  }
  return <VariantPrices locationId={scope.locationId} variantId={variantId} />;
}

function VariantPrices({ locationId, variantId }: { locationId: Id; variantId: Id }) {
  const scope = useLocationScope();
  const row = usePrices({ fulfillmentLocationId: locationId, variantId, page: 1, pageSize: 1 });
  const schedule = usePriceSchedule(locationId, variantId);
  const item = row.data?.items[0];
  const [setting, setSetting] = useState(false);
  const [stopping, setStopping] = useState(false);
  const end = useEndPrice();
  const title = item ? `${item.productName} · ${item.variantName}` : "Variant price";

  return (
    <>
      <PageHeader
        title={title}
        description={`${item ? `${item.sku} · ${PRICE_UNIT[item.priceUnit] ?? item.priceUnit} · ` : ""}${scope.nameOf(locationId)}`}
        backTo="/pricing"
        backLabel="Prices"
        actions={
          schedule.data && (
            <>
              {schedule.data.current && schedule.data.version && (
                <Button variant="outline" onClick={() => setStopping(true)}>
                  Stop selling here
                </Button>
              )}
              <Button onClick={() => setSetting(true)}>Set price</Button>
            </>
          )
        }
      />
      <div className="flex flex-col gap-4">
        <QueryView query={schedule} what="price">
          {(data) => (
            <Section
              title="Price periods"
              description="Times are India time. The server picks the period in force."
            >
              {data.history.length === 0 && !data.current && !data.next ? (
                <EmptyState
                  title="Never priced here"
                  description="Set a price to put this variant on sale."
                />
              ) : (
                <PeriodList
                  periods={dedupe([
                    ...(data.next ? [data.next] : []),
                    ...(data.current ? [data.current] : []),
                    ...data.history,
                  ])}
                  currentId={data.current?.id}
                  nextId={data.next?.id}
                  render={(p: PricePeriod) => (
                    <>
                      <strong className="tabular-nums">{formatRupees(p.unitPricePaise)}</strong>
                      {p.compareAtPaise != null && (
                        <span className="text-text-muted ml-2 line-through">
                          {formatRupees(p.compareAtPaise)}
                        </span>
                      )}
                    </>
                  )}
                />
              )}
            </Section>
          )}
        </QueryView>
        <ChargesSection locationId={locationId} variantId={variantId} productId={item?.productId} />
      </div>
      {setting && (
        <SetPriceDialog
          locationId={locationId}
          variantId={variantId}
          title={title}
          priceUnit={item?.priceUnit ?? ""}
          onClose={() => setSetting(false)}
        />
      )}
      <Dialog open={stopping} onOpenChange={setStopping}>
        <DialogContent
          size="sm"
          title="Stop selling here?"
          description="The current price ends now and scheduled prices are removed. The variant is no longer for sale at this location until a new price is set."
          footer={
            <>
              <Button variant="outline" onClick={() => setStopping(false)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                loading={end.isPending}
                onClick={() => {
                  if (!schedule.data?.version) return;
                  end.mutate(
                    { locationId, variantId, version: schedule.data.version },
                    {
                      onSuccess: () => {
                        notifySaved(
                          "Stopped selling",
                          "The variant has no price at this location now.",
                        );
                        setStopping(false);
                      },
                      onError: (error) => {
                        setStopping(false);
                        reportError(error, {
                          onConflict: () => schedule.refetch(),
                          title: "Not stopped",
                        });
                      },
                    },
                  );
                }}
              >
                Stop selling
              </Button>
            </>
          }
        />
      </Dialog>
    </>
  );
}

function dedupe<T extends { id: string }>(periods: T[]): T[] {
  const seen = new Set<string>();
  return periods.filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)));
}

function PeriodList<T extends { id: string; validFrom: string; validTo: string | null }>({
  periods,
  currentId,
  nextId,
  render,
}: {
  periods: T[];
  currentId: string | undefined;
  nextId: string | undefined;
  render: (period: T) => React.ReactNode;
}) {
  return (
    <ul className="divide-border flex flex-col divide-y">
      {periods.map((p) => (
        <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
          <span>{render(p)}</span>
          <span className="text-text-muted flex items-center gap-2">
            {periodText(p.validFrom, p.validTo)}
            {p.id === currentId && <Badge tone="success">Current</Badge>}
            {p.id === nextId && <Badge tone="info">Scheduled</Badge>}
          </span>
        </li>
      ))}
    </ul>
  );
}

/** Preparation charges of this variant at the location (e.g. cleaning ₹40 per kg). */
function ChargesSection({
  locationId,
  variantId,
  productId,
}: {
  locationId: Id;
  variantId: Id;
  productId: Id | undefined;
}) {
  const can = useCan();
  const canReadCatalog = can("catalog.manage");
  const product = useProduct(productId ?? "", canReadCatalog && productId !== undefined);
  // Without the catalogue permission every preparation method is offered (G3); the server keeps the variant's own.
  const methods = usePreparationMethods(!canReadCatalog);
  const own = product.data?.variants.find((v) => v.id === variantId)?.preparations;
  const known: { code: string; name: string }[] = (canReadCatalog ? own : methods.data) ?? [];
  const [typed, setTyped] = useState("");
  const [picked, setPicked] = useState("");
  const code = (known.length > 0 ? picked : typed).trim().toUpperCase();

  return (
    <Section
      title="Preparation charges"
      description="Extra charged for a preparation at this location. No charge set = the preparation is free."
    >
      {known.length > 0 ? (
        <div className="w-72">
          <FormField label="Preparation">
            <Select
              options={known.map((p) => ({ value: p.code, label: `${p.name} (${p.code})` }))}
              value={picked}
              placeholder="Choose a preparation"
              onValueChange={setPicked}
            />
          </FormField>
        </div>
      ) : (
        <div className="w-72">
          <FormField label="Preparation code" hint="e.g. CLEANED_CUT">
            <Input value={typed} onChange={(e) => setTyped(e.target.value)} />
          </FormField>
        </div>
      )}
      {code ? (
        <ChargeSchedulePanel key={code} locationId={locationId} variantId={variantId} code={code} />
      ) : (
        <p className="text-text-muted text-sm">Choose a preparation to see and set its charge.</p>
      )}
    </Section>
  );
}

function ChargeSchedulePanel({
  locationId,
  variantId,
  code,
}: {
  locationId: Id;
  variantId: Id;
  code: string;
}) {
  const charge = useChargeSchedule(locationId, variantId, code);
  const [open, setOpen] = useState(false);
  if (charge.isPending) return <Skeleton className="h-16 w-full" />;
  return (
    <QueryView query={charge} what="preparation charge">
      {(data) => (
        <div className="flex flex-col gap-3">
          {!data.current && !data.next && data.history.length === 0 ? (
            <p className="text-sm">No charge set — this preparation is free here.</p>
          ) : (
            <PeriodList
              periods={dedupe([
                ...(data.next ? [data.next] : []),
                ...(data.current ? [data.current] : []),
                ...data.history,
              ])}
              currentId={data.current?.id}
              nextId={data.next?.id}
              render={(p: ChargePeriod) => (
                <>
                  <strong className="tabular-nums">{formatRupees(p.chargePaise)}</strong>
                  <span className="text-text-muted ml-2">{CHARGE_BASIS[p.chargeBasis]}</span>
                </>
              )}
            />
          )}
          <Button variant="outline" className="w-fit" onClick={() => setOpen(true)}>
            Set charge
          </Button>
          {open && (
            <SetChargeDialog
              locationId={locationId}
              variantId={variantId}
              code={code}
              version={data.version}
              onClose={() => setOpen(false)}
            />
          )}
        </div>
      )}
    </QueryView>
  );
}
