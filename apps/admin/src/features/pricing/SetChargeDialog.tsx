import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent } from "@fresh/ui";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useSetCharge } from "../../api/pricing";
import { type Id, type Version } from "../../api/types/common";
import { type ChargeBasis } from "../../api/types/pricing";
import { SelectField, TextField } from "../../components/form";
import { notifySaved, reportError } from "../../lib/feedback";
import { istLocalToUtcIso, parseRupeesToPaise } from "../../lib/units";
import { CHARGE_BASIS, CheckboxField, StartField, hasCode } from "./shared";

const schema = z.object({
  charge: z
    .string()
    .refine((v) => parseRupeesToPaise(v) !== null, "Enter the charge in rupees, e.g. 40."),
  chargeBasis: z.enum(["PER_KG", "PER_ITEM", "FLAT"]),
  validFrom: z
    .string()
    .refine((v) => v === "" || istLocalToUtcIso(v) !== null, "Enter a valid start date and time."),
  replaceFuture: z.boolean(),
});

type ChargeValues = z.infer<typeof schema>;

const FIELDS = ["charge", "chargeBasis", "validFrom", "replaceFuture"] as const;

export function SetChargeDialog({
  locationId,
  variantId,
  code,
  version,
  onClose,
}: {
  locationId: Id;
  variantId: Id;
  code: string;
  version: Version | null;
  onClose: () => void;
}) {
  const setCharge = useSetCharge();
  const form = useForm<ChargeValues>({
    resolver: zodResolver(schema),
    defaultValues: { charge: "", chargeBasis: "PER_KG", validFrom: "", replaceFuture: false },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await setCharge.mutateAsync({
        locationId,
        variantId,
        code,
        version,
        body: {
          chargePaise: parseRupeesToPaise(values.charge) ?? 0,
          chargeBasis: values.chargeBasis,
          validFrom: values.validFrom === "" ? null : istLocalToUtcIso(values.validFrom),
          replaceFuture: values.replaceFuture,
        },
      });
      notifySaved("Charge saved");
      onClose();
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        aliases: { chargePaise: "charge" },
        title: "Charge not saved",
        onConflict: onClose,
      });
      if (hasCode(error, "PRICE_PERIOD_OVERLAP")) {
        form.setError("replaceFuture", {
          type: "PRICE_PERIOD_OVERLAP",
          message: "A later charge is already scheduled. Tick this to replace it.",
        });
      }
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={`Set charge · ${code}`}
        description="Set ₹0 to make the preparation free."
      >
        <form noValidate onSubmit={submit} className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <TextField form={form} name="charge" label="Charge (₹)" required inputMode="decimal" />
            <SelectField
              form={form}
              name="chargeBasis"
              label="Charged"
              required
              options={(Object.keys(CHARGE_BASIS) as ChargeBasis[]).map((b) => ({
                value: b,
                label: CHARGE_BASIS[b],
              }))}
            />
          </div>
          <StartField form={form} name="validFrom" />
          <CheckboxField form={form} name="replaceFuture" label="Replace scheduled charges" />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={form.formState.isSubmitting}>
              Save charge
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
