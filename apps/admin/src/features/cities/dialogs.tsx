import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, type SelectOption } from "@fresh/ui";
import { type FieldPath, type UseFormReturn, useForm } from "react-hook-form";
import { useNavigate } from "react-router";
import { z } from "zod";
import { useCreateFulfillmentLocation, useCreateServiceArea } from "../../api/locations";
import { SelectField, SwitchField, TextField } from "../../components/form";
import { notifySaved } from "../../lib/feedback";
import { parseRupeesToPaise } from "../../lib/units";
import { LOCATION_FIELD_NAMES, locationFields, optionalRupees, rupees } from "./schemas";
import { emptyToNull, optionalNumber, reportWriteError } from "./shared";

const createLocationSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^[A-Z][A-Z0-9_]{1,31}$/, "Enter a code in capitals, e.g. TST_NORTH."),
  ...locationFields,
});

export function CreateLocationDialog({ cityId, onClose }: { cityId: string; onClose: () => void }) {
  const create = useCreateFulfillmentLocation();
  const navigate = useNavigate();
  const form = useForm<z.infer<typeof createLocationSchema>>({
    resolver: zodResolver(createLocationSchema),
    defaultValues: {
      code: "",
      name: "",
      addressLine: "",
      pincode: "",
      phone: "",
      latitude: "",
      longitude: "",
      hasColdStorage: true,
      fssaiLicenseNo: "",
    },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      const location = await create.mutateAsync({
        cityId,
        code: v.code,
        name: v.name,
        addressLine: emptyToNull(v.addressLine),
        pincode: emptyToNull(v.pincode),
        phone: emptyToNull(v.phone),
        latitude: optionalNumber(v.latitude),
        longitude: optionalNumber(v.longitude),
        hasColdStorage: v.hasColdStorage,
        fssaiLicenseNo: emptyToNull(v.fssaiLicenseNo),
      });
      notifySaved("Location added", "Set its opening hours and service areas next.");
      onClose();
      void navigate(`/locations/${location.id}`);
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: LOCATION_FIELD_NAMES,
        codeFields: { DUPLICATE: "code" },
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        title="New fulfillment location"
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="new-location" loading={create.isPending}>
              Add location
            </Button>
          </>
        }
      >
        <form id="new-location" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <TextField form={form} name="name" label="Name" required />
            <TextField form={form} name="code" label="Code" required autoComplete="off" />
          </div>
          <TextField form={form} name="addressLine" label="Address" />
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <TextField form={form} name="pincode" label="Pincode" inputMode="numeric" />
            <TextField form={form} name="phone" label="Phone" type="tel" />
            <TextField form={form} name="latitude" label="Latitude" inputMode="decimal" />
            <TextField form={form} name="longitude" label="Longitude" inputMode="decimal" />
          </div>
          <TextField
            form={form}
            name="fssaiLicenseNo"
            label="FSSAI licence number"
            inputMode="numeric"
          />
          <SwitchField form={form} name="hasColdStorage" label="Has cold storage" />
        </form>
      </DialogContent>
    </Dialog>
  );
}

const createAreaSchema = z.object({
  fulfillmentLocationId: z.string().min(1, "Choose the location that delivers to this area."),
  name: z.string().trim().min(1, "Enter the service area name.").max(120),
  deliveryFee: rupees("Enter the delivery fee in rupees, e.g. 49."),
  freeDeliveryAbove: optionalRupees,
  minOrder: rupees("Enter the minimum order in rupees, e.g. 299."),
});

/** New service area of a city. `locations` lists the city's locations; a single one is preselected. */
export function CreateServiceAreaDialog({
  cityId,
  locations,
  onClose,
}: {
  cityId: string;
  locations: readonly SelectOption[];
  onClose: () => void;
}) {
  const create = useCreateServiceArea();
  const navigate = useNavigate();
  const form = useForm<z.infer<typeof createAreaSchema>>({
    resolver: zodResolver(createAreaSchema),
    defaultValues: {
      fulfillmentLocationId: locations.length === 1 ? (locations[0]?.value ?? "") : "",
      name: "",
      deliveryFee: "",
      freeDeliveryAbove: "",
      minOrder: "",
    },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      const area = await create.mutateAsync({
        cityId,
        fulfillmentLocationId: v.fulfillmentLocationId,
        name: v.name,
        deliveryFeePaise: parseRupeesToPaise(v.deliveryFee) ?? 0,
        freeDeliveryAbovePaise:
          v.freeDeliveryAbove === "" ? null : parseRupeesToPaise(v.freeDeliveryAbove),
        minOrderPaise: parseRupeesToPaise(v.minOrder) ?? 0,
      });
      notifySaved("Service area added", "Add its pincodes next.");
      onClose();
      void navigate(`/service-areas/${area.id}`);
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["fulfillmentLocationId", "name", "deliveryFee", "freeDeliveryAbove", "minOrder"],
        aliases: {
          deliveryFeePaise: "deliveryFee",
          freeDeliveryAbovePaise: "freeDeliveryAbove",
          minOrderPaise: "minOrder",
        },
        codeFields: { DUPLICATE: "name" },
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        title="New service area"
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="new-service-area" loading={create.isPending}>
              Add service area
            </Button>
          </>
        }
      >
        <form id="new-service-area" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <SelectField
            form={form}
            name="fulfillmentLocationId"
            label="Delivered from"
            required
            options={locations}
            placeholder={locations.length === 0 ? "Add a location first" : "Choose a location"}
          />
          <TextField form={form} name="name" label="Name" required />
          <ServiceAreaFeeFields form={form} />
        </form>
      </DialogContent>
    </Dialog>
  );
}

interface FeeValues {
  deliveryFee: string;
  freeDeliveryAbove: string;
  minOrder: string;
}

/** Fee inputs shared by the create and edit forms (rupees typed, paise sent). */
export function ServiceAreaFeeFields<T extends FeeValues>({ form }: { form: UseFormReturn<T> }) {
  const name = (field: keyof FeeValues) => field as FieldPath<T>;
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
      <TextField
        form={form}
        name={name("deliveryFee")}
        label="Delivery fee (₹)"
        inputMode="decimal"
        required
      />
      <TextField
        form={form}
        name={name("freeDeliveryAbove")}
        label="Free delivery above (₹)"
        hint="Leave empty for no free delivery."
        inputMode="decimal"
      />
      <TextField
        form={form}
        name={name("minOrder")}
        label="Minimum order (₹)"
        inputMode="decimal"
        required
      />
    </div>
  );
}
