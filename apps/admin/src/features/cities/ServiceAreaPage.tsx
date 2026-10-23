import { formatDateTime, isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, StatusBadge } from "@fresh/ui";
import { X } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useParams } from "react-router";
import { z } from "zod";
import {
  useAddPincodes,
  useFulfillmentLocation,
  usePincodes,
  useRemovePincode,
  useServiceArea,
  useUpdateServiceArea,
} from "../../api/locations";
import { type ServiceArea } from "../../api/types/locations";
import { SwitchField, TextAreaField, TextField } from "../../components/form";
import { PageHeader, QueryView, Section } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { ACTIVE_FLAG } from "../../lib/status";
import { paiseToRupeesInput, parseRupeesToPaise } from "../../lib/units";
import { ServiceAreaFeeFields } from "./dialogs";
import { optionalRupees, rupees } from "./schemas";
import { reportWriteError } from "./shared";

export function ServiceAreaPage() {
  const { serviceAreaId = "" } = useParams();
  const area = useServiceArea(serviceAreaId);
  return (
    <QueryView query={area} what="service area">
      {(a) => <ServiceAreaDetail area={a} onReload={() => void area.refetch()} />}
    </QueryView>
  );
}

function ServiceAreaDetail({ area, onReload }: { area: ServiceArea; onReload: () => void }) {
  const location = useFulfillmentLocation(area.fulfillmentLocationId);
  return (
    <>
      <PageHeader
        backTo={`/cities/${area.cityId}#service-areas`}
        backLabel="City"
        title={area.name}
        meta={<StatusBadge code={String(area.isActive)} map={ACTIVE_FLAG} />}
        description={location.data ? `Delivered from ${location.data.name}` : undefined}
      />
      <div className="flex flex-col gap-6">
        <AreaForm key={area.version} area={area} onReload={onReload} />
        <Pincodes area={area} onReload={onReload} />
      </div>
    </>
  );
}

const areaSchema = z.object({
  name: z.string().trim().min(1, "Enter the service area name.").max(120),
  deliveryFee: rupees("Enter the delivery fee in rupees, e.g. 49."),
  freeDeliveryAbove: optionalRupees,
  minOrder: rupees("Enter the minimum order in rupees, e.g. 299."),
  isActive: z.boolean(),
});

function AreaForm({ area, onReload }: { area: ServiceArea; onReload: () => void }) {
  const update = useUpdateServiceArea(area.id);
  const form = useForm<z.infer<typeof areaSchema>>({
    resolver: zodResolver(areaSchema),
    defaultValues: {
      name: area.name,
      deliveryFee: paiseToRupeesInput(area.deliveryFeePaise),
      freeDeliveryAbove: paiseToRupeesInput(area.freeDeliveryAbovePaise),
      minOrder: paiseToRupeesInput(area.minOrderPaise),
      isActive: area.isActive,
    },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      await update.mutateAsync({
        version: area.version,
        body: {
          name: v.name,
          deliveryFeePaise: parseRupeesToPaise(v.deliveryFee) ?? 0,
          freeDeliveryAbovePaise:
            v.freeDeliveryAbove === "" ? null : parseRupeesToPaise(v.freeDeliveryAbove),
          minOrderPaise: parseRupeesToPaise(v.minOrder) ?? 0,
          isActive: v.isActive,
        },
      });
      notifySaved("Service area saved");
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["name", "deliveryFee", "freeDeliveryAbove", "minOrder", "isActive"],
        aliases: {
          deliveryFeePaise: "deliveryFee",
          freeDeliveryAbovePaise: "freeDeliveryAbove",
          minOrderPaise: "minOrder",
        },
        codeFields: { DUPLICATE: "name" },
        onConflict: onReload,
      });
    }
  });
  return (
    <Section
      title="Fees"
      description="The server applies these at checkout; the apps only show them."
      actions={
        <Button
          type="submit"
          form="service-area"
          loading={update.isPending}
          disabled={!form.formState.isDirty}
        >
          Save
        </Button>
      }
    >
      <form id="service-area" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
        <TextField form={form} name="name" label="Name" required />
        <ServiceAreaFeeFields form={form} />
        <SwitchField
          form={form}
          name="isActive"
          label="Active"
          description="Inactive areas do not accept orders for their pincodes."
        />
      </form>
    </Section>
  );
}

const pincodeSchema = z.object({
  pincodes: z
    .string()
    .trim()
    .min(1, "Enter one or more 6-digit pincodes.")
    .refine(
      (v) => splitPincodes(v).every((p) => /^[1-9]\d{5}$/.test(p)),
      "Enter 6-digit pincodes separated by commas or spaces.",
    ),
});

function splitPincodes(text: string): string[] {
  return [...new Set(text.split(/[\s,]+/).filter(Boolean))];
}

function Pincodes({ area, onReload }: { area: ServiceArea; onReload: () => void }) {
  const [includeRemoved, setIncludeRemoved] = useState(false);
  const pincodes = usePincodes(area.id, includeRemoved);
  const add = useAddPincodes(area.id);
  const remove = useRemovePincode(area.id);
  const form = useForm<z.infer<typeof pincodeSchema>>({
    resolver: zodResolver(pincodeSchema),
    defaultValues: { pincodes: "" },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      const result = await add.mutateAsync({
        pincodes: splitPincodes(v.pincodes),
        version: area.version,
      });
      notifySaved(
        result.added.length === 1 ? "1 pincode added" : `${result.added.length} pincodes added`,
        `${result.activePincodeCount} active in this area.`,
      );
      form.reset();
    } catch (error) {
      if (isApiError(error) && error.code === "PINCODE_ALREADY_ASSIGNED") {
        form.setError(
          "pincodes",
          { type: error.code, message: errorMessage(error) },
          { shouldFocus: true },
        );
        return;
      }
      reportWriteError(error, {
        setError: form.setError,
        fields: ["pincodes"],
        onConflict: onReload,
      });
    }
  });
  const items = pincodes.data ?? [];
  return (
    <Section
      title="Pincodes"
      description={`${area.activePincodeCount} active. A pincode belongs to one service area at a time.`}
      actions={
        <Button
          size="sm"
          variant="ghost"
          aria-pressed={includeRemoved}
          onClick={() => setIncludeRemoved((v) => !v)}
        >
          {includeRemoved ? "Hide removed" : "Show removed"}
        </Button>
      }
    >
      <form noValidate onSubmit={onSubmit} className="flex flex-col gap-3">
        <TextAreaField
          form={form}
          name="pincodes"
          label="Add pincodes"
          hint="Separate with commas, spaces or new lines."
          rows={2}
        />
        <Button type="submit" variant="outline" className="w-fit" loading={add.isPending}>
          Add pincodes
        </Button>
      </form>
      {pincodes.isPending ? (
        <p className="text-text-muted text-sm">Loading pincodes…</p>
      ) : pincodes.error ? (
        <p role="alert" className="text-danger-text text-sm">
          {errorMessage(pincodes.error)}
        </p>
      ) : items.length === 0 ? (
        <p className="text-text-muted text-sm">
          No pincodes yet. Customers can order only from listed pincodes.
        </p>
      ) : (
        <ul aria-label="Pincodes" className="flex flex-wrap gap-2">
          {items.map((p) => (
            <li
              key={p.pincode}
              title={`Added ${formatDateTime(p.addedAt)}`}
              className={`border-border inline-flex items-center gap-1 rounded-full border py-0.5 pr-1 pl-3 text-sm ${
                p.isActive ? "" : "text-text-muted line-through"
              }`}
            >
              {p.pincode}
              {p.isActive && (
                <Button
                  size="icon-sm"
                  variant="ghost"
                  aria-label={`Remove pincode ${p.pincode}`}
                  disabled={remove.isPending}
                  onClick={() =>
                    remove.mutate(
                      { pincode: p.pincode, version: area.version },
                      {
                        onSuccess: () => notifySaved(`Pincode ${p.pincode} removed`),
                        onError: (error) =>
                          reportError(error, {
                            title: "Could not remove the pincode",
                            onConflict: onReload,
                          }),
                      },
                    )
                  }
                >
                  <X aria-hidden="true" />
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}
