import { formatRupees } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, Skeleton, toast } from "@fresh/ui";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { usePriceSchedule, useSetPrice } from "../../api/pricing";
import { type Id } from "../../api/types/common";
import { TextField } from "../../components/form";
import { notifySaved, reportError } from "../../lib/feedback";
import { istLocalToUtcIso, parseRupeesToPaise } from "../../lib/units";
import { CheckboxField, PRICE_UNIT, StartField, hasCode, periodText } from "./shared";

const rupees = (message: string) =>
  z.string().refine((v) => parseRupeesToPaise(v) !== null, message);

const schema = z.object({
  price: rupees("Enter the price in rupees, e.g. 1249 or 1249.50."),
  compareAt: z
    .string()
    .refine(
      (v) => v.trim() === "" || parseRupeesToPaise(v) !== null,
      "Enter the compare-at price in rupees, or leave it empty.",
    ),
  validFrom: z
    .string()
    .refine((v) => v === "" || istLocalToUtcIso(v) !== null, "Enter a valid start date and time."),
  reason: z.string().max(500, "Use at most 500 characters."),
  replaceFuture: z.boolean(),
});

type PriceValues = z.infer<typeof schema>;

const FIELDS = ["price", "compareAt", "validFrom", "reason", "replaceFuture"] as const;
const ALIASES = { unitPricePaise: "price", compareAtPaise: "compareAt" };

/** Set (or schedule) the price of one variant at one location. Loads the schedule first for its version. */
export function SetPriceDialog({
  locationId,
  variantId,
  title,
  priceUnit,
  onClose,
}: {
  locationId: Id;
  variantId: Id;
  title: string;
  priceUnit: string;
  onClose: () => void;
}) {
  const schedule = usePriceSchedule(locationId, variantId);
  const setPrice = useSetPrice();
  const form = useForm<PriceValues>({
    resolver: zodResolver(schema),
    defaultValues: { price: "", compareAt: "", validFrom: "", reason: "", replaceFuture: false },
  });

  const submit = form.handleSubmit(async (values) => {
    if (!schedule.data) return;
    try {
      const result = await setPrice.mutateAsync({
        locationId,
        variantId,
        version: schedule.data.version,
        body: {
          unitPricePaise: parseRupeesToPaise(values.price) ?? 0,
          compareAtPaise:
            values.compareAt.trim() === "" ? null : parseRupeesToPaise(values.compareAt),
          validFrom: values.validFrom === "" ? null : istLocalToUtcIso(values.validFrom),
          reason: values.reason.trim() || null,
          replaceFuture: values.replaceFuture,
        },
      });
      if (result.unchanged) {
        toast({
          title: "Price unchanged",
          description: "This is already the current price.",
          tone: "info",
        });
      } else {
        notifySaved(
          "Price saved",
          result.largeChange
            ? "This is a change of more than 50 % — check it is intended."
            : undefined,
        );
      }
      onClose();
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        aliases: ALIASES,
        title: "Price not saved",
        onConflict: () => schedule.refetch(),
      });
      if (hasCode(error, "PRICE_PERIOD_OVERLAP")) {
        form.setError("replaceFuture", {
          type: "PRICE_PERIOD_OVERLAP",
          message:
            "A later price is already scheduled. Tick this to replace it, or choose an earlier start.",
        });
      }
    }
  });

  const current = schedule.data?.current;
  const next = schedule.data?.next;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={`Set price · ${title}`}
        description={`Price ${PRICE_UNIT[priceUnit] ?? priceUnit}, including tax.`}
      >
        {schedule.isPending ? (
          <div aria-busy="true" className="flex flex-col gap-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : schedule.error ? (
          <p role="alert" className="text-danger-text text-sm">
            Could not load the current price. Close this and try again.
          </p>
        ) : (
          <form noValidate onSubmit={submit} className="flex flex-col gap-4">
            <div className="bg-muted rounded-md p-3 text-sm">
              <p>
                Current:{" "}
                {current ? (
                  <strong>{formatRupees(current.unitPricePaise)}</strong>
                ) : (
                  <strong>No price — not for sale here</strong>
                )}
                {current && (
                  <span className="text-text-muted">
                    {" "}
                    · {periodText(current.validFrom, current.validTo)}
                  </span>
                )}
              </p>
              {next && (
                <p>
                  Scheduled: <strong>{formatRupees(next.unitPricePaise)}</strong>
                  <span className="text-text-muted">
                    {" "}
                    · from {periodText(next.validFrom, next.validTo)}
                  </span>
                </p>
              )}
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <TextField form={form} name="price" label="Price (₹)" required inputMode="decimal" />
              <TextField
                form={form}
                name="compareAt"
                label="Compare-at price (₹)"
                inputMode="decimal"
                hint="Optional struck-through price; must be higher."
              />
            </div>
            <StartField form={form} name="validFrom" />
            <TextField
              form={form}
              name="reason"
              label="Reason"
              hint="Optional, kept in the audit log."
            />
            <CheckboxField
              form={form}
              name="replaceFuture"
              label="Replace scheduled prices"
              description="Remove any price scheduled to start after this one."
            />
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" loading={form.formState.isSubmitting}>
                Save price
              </Button>
            </div>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
