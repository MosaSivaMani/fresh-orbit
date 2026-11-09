import { formatRupees } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@fresh/ui";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useLocationCostRates, useSaveLocationCostRates } from "../../api/delivery";
import { type LocationCostRates } from "../../api/types/delivery";
import { TextField } from "../../components/form";
import { QueryView, Section } from "../../components/page";
import { notifySaved } from "../../lib/feedback";
import { paiseToRupeesInput, parseRupeesToPaise } from "../../lib/units";
import { reportWriteError } from "./shared";

// D365: what one delivered order costs this location beyond the fish. Staff type rupees; paise are sent. An empty
// field is "not configured": orders delivered then record no cost and the contribution margin report says so.

/** Server limits (D365), repeated here only to say so before the request; the server refuses anything else. */
const MAX_DELIVERY_PAISE = 100_000;
const MAX_PACKAGING_PAISE = 50_000;

const NOT_CONFIGURED_NOTE =
  "Not configured. Orders delivered while a cost is empty show “Not configured” in the contribution margin report instead of a number.";
const SNAPSHOT_NOTE =
  "Each delivery records the costs set at that moment; changing them later does not change past orders.";

const optionalCost = (maxPaise: number, message: string) =>
  z
    .string()
    .trim()
    .refine((v) => {
      if (v === "") return true;
      const paise = parseRupeesToPaise(v);
      return paise !== null && paise <= maxPaise;
    }, message);

const costSchema = z.object({
  deliveryCost: optionalCost(
    MAX_DELIVERY_PAISE,
    `Enter the rider pay per delivery in rupees, up to ${formatRupees(MAX_DELIVERY_PAISE)}, or leave it empty.`,
  ),
  packagingCost: optionalCost(
    MAX_PACKAGING_PAISE,
    `Enter the packaging cost per order in rupees, up to ${formatRupees(MAX_PACKAGING_PAISE)}, or leave it empty.`,
  ),
});

type CostValues = z.infer<typeof costSchema>;

const toPaise = (text: string): number | null =>
  text.trim() === "" ? null : parseRupeesToPaise(text);

export function LocationCostRatesSection({ locationId }: { locationId: string }) {
  const rates = useLocationCostRates(locationId);
  return (
    <QueryView query={rates} what="delivery and packaging costs">
      {(r) => (
        <CostRatesForm
          key={r.version ?? "new"}
          rates={r}
          locationId={locationId}
          onReload={() => void rates.refetch()}
        />
      )}
    </QueryView>
  );
}

function CostRatesForm({
  rates,
  locationId,
  onReload,
}: {
  rates: LocationCostRates;
  locationId: string;
  onReload: () => void;
}) {
  const save = useSaveLocationCostRates(locationId);
  const form = useForm<CostValues>({
    resolver: zodResolver(costSchema),
    defaultValues: {
      deliveryCost: paiseToRupeesInput(rates.deliveryCostPerDropPaise),
      packagingCost: paiseToRupeesInput(rates.packagingCostPerOrderPaise),
    },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      await save.mutateAsync({
        version: rates.version,
        body: {
          deliveryCostPerDropPaise: toPaise(v.deliveryCost),
          packagingCostPerOrderPaise: toPaise(v.packagingCost),
        },
      });
      notifySaved("Costs saved", "Orders delivered from now on record these costs.");
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["deliveryCost", "packagingCost"],
        aliases: {
          deliveryCostPerDropPaise: "deliveryCost",
          packagingCostPerOrderPaise: "packagingCost",
        },
        onConflict: onReload,
      });
    }
  });
  return (
    <Section
      title="Delivery and packaging cost"
      description="What one delivered order costs this location, used by the contribution margin report."
      actions={
        <Button
          type="submit"
          form="location-costs"
          loading={save.isPending}
          disabled={rates.version !== null && !form.formState.isDirty}
        >
          Save costs
        </Button>
      }
    >
      <form id="location-costs" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
        {!rates.configured && (
          <p
            role="status"
            className="bg-warning-subtle text-warning-text rounded-md px-3 py-2 text-sm"
          >
            {NOT_CONFIGURED_NOTE}
          </p>
        )}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <TextField
            form={form}
            name="deliveryCost"
            label="Rider pay per delivery (₹)"
            hint="Paid per drop. Leave empty if not known yet."
            inputMode="decimal"
          />
          <TextField
            form={form}
            name="packagingCost"
            label="Packaging per order (₹)"
            hint="Insulated bag, ice and label. Leave empty if not known yet."
            inputMode="decimal"
          />
        </div>
        <p className="text-text-muted text-xs">{SNAPSHOT_NOTE}</p>
      </form>
    </Section>
  );
}
