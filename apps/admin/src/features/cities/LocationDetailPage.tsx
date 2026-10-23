import { formatCalendarDate, isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Checkbox, Input, StatusBadge } from "@fresh/ui";
import { Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useParams } from "react-router";
import { z } from "zod";
import {
  useAddClosure,
  useClosures,
  useFulfillmentLocation,
  useRemoveClosure,
  useServiceAreas,
  useSetOperatingHours,
  useUpdateFulfillmentLocation,
} from "../../api/locations";
import { type DayOfWeekName, type FulfillmentLocation } from "../../api/types/locations";
import { SelectField, SwitchField, TextField } from "../../components/form";
import { PageHeader, QueryView, Section } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { LOCATION_STATUS } from "../../lib/status";
import { todayInIndia } from "../../lib/units";
import { ServiceAreaList } from "./CityDetailPage";
import { CreateServiceAreaDialog } from "./dialogs";
import { LOCATION_FIELD_NAMES, locationFields } from "./schemas";
import {
  FormAlert,
  LOCATION_STATES,
  WEEK,
  emptyToNull,
  optionalNumber,
  reportWriteError,
} from "./shared";

export function LocationDetailPage() {
  const { locationId = "" } = useParams();
  const location = useFulfillmentLocation(locationId);
  return (
    <QueryView query={location} what="location">
      {(l) => (
        <>
          <PageHeader
            backTo={`/cities/${l.cityId}`}
            backLabel="City"
            title={l.name}
            meta={<StatusBadge code={l.status} map={LOCATION_STATUS} />}
            description={l.code}
          />
          <div className="flex flex-col gap-6">
            <LocationForm key={l.version} location={l} onReload={() => void location.refetch()} />
            <OperatingHours
              key={`hours-${l.version}`}
              location={l}
              onReload={() => void location.refetch()}
            />
            <Closures locationId={l.id} />
            <LocationServiceAreas location={l} />
          </div>
        </>
      )}
    </QueryView>
  );
}

const locationSchema = z.object({
  ...locationFields,
  status: z.enum(["DRAFT", "ACTIVE", "PAUSED"]),
});

function LocationForm({
  location,
  onReload,
}: {
  location: FulfillmentLocation;
  onReload: () => void;
}) {
  const update = useUpdateFulfillmentLocation(location.id);
  const form = useForm<z.infer<typeof locationSchema>>({
    resolver: zodResolver(locationSchema),
    defaultValues: {
      name: location.name,
      addressLine: location.addressLine ?? "",
      pincode: location.pincode ?? "",
      phone: location.phone ?? "",
      latitude: location.latitude == null ? "" : String(location.latitude),
      longitude: location.longitude == null ? "" : String(location.longitude),
      hasColdStorage: location.hasColdStorage,
      fssaiLicenseNo: location.fssaiLicenseNo ?? "",
      status: location.status,
    },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      await update.mutateAsync({
        version: location.version,
        body: {
          name: v.name,
          addressLine: emptyToNull(v.addressLine),
          pincode: emptyToNull(v.pincode),
          phone: emptyToNull(v.phone),
          latitude: optionalNumber(v.latitude),
          longitude: optionalNumber(v.longitude),
          hasColdStorage: v.hasColdStorage,
          fssaiLicenseNo: emptyToNull(v.fssaiLicenseNo),
          status: v.status === location.status ? null : v.status,
        },
      });
      notifySaved("Location saved");
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: LOCATION_FIELD_NAMES,
        onConflict: onReload,
      });
    }
  });
  return (
    <Section
      title="Details"
      actions={
        <Button
          type="submit"
          form="location-details"
          loading={update.isPending}
          disabled={!form.formState.isDirty}
        >
          Save details
        </Button>
      }
    >
      <form id="location-details" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <TextField form={form} name="name" label="Name" required />
          <SelectField
            form={form}
            name="status"
            label="Status"
            required
            options={LOCATION_STATES}
          />
        </div>
        <TextField form={form} name="addressLine" label="Address" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-4">
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
    </Section>
  );
}

interface DayRow {
  open: boolean;
  opensAt: string;
  closesAt: string;
}

/** Seven-day editor; the whole week is sent in one PUT (a closed day has no hours). */
function OperatingHours({
  location,
  onReload,
}: {
  location: FulfillmentLocation;
  onReload: () => void;
}) {
  const save = useSetOperatingHours(location.id);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [rows, setRows] = useState<Record<DayOfWeekName, DayRow>>(() => {
    const byDay = new Map(location.operatingHours.map((h) => [h.dayOfWeek, h]));
    return Object.fromEntries(
      WEEK.map(({ day }) => {
        const h = byDay.get(day);
        return [
          day,
          {
            open: Boolean(h),
            opensAt: h?.opensAt.slice(0, 5) ?? "",
            closesAt: h?.closesAt.slice(0, 5) ?? "",
          },
        ];
      }),
    ) as Record<DayOfWeekName, DayRow>;
  });
  const setRow = (day: DayOfWeekName, patch: Partial<DayRow>) =>
    setRows((r) => ({ ...r, [day]: { ...r[day], ...patch } }));

  const submit = async () => {
    setRefusal(null);
    const incomplete = WEEK.find(
      ({ day }) => rows[day].open && (!rows[day].opensAt || !rows[day].closesAt),
    );
    if (incomplete) {
      setRefusal(`Enter opening and closing times for ${incomplete.label}, or mark it closed.`);
      return;
    }
    try {
      await save.mutateAsync({
        version: location.version,
        body: {
          days: WEEK.filter(({ day }) => rows[day].open).map(({ day }) => ({
            dayOfWeek: day,
            opensAt: rows[day].opensAt,
            closesAt: rows[day].closesAt,
          })),
        },
      });
      notifySaved("Opening hours saved");
    } catch (error) {
      if (isApiError(error) && error.status === 422) {
        setRefusal(errorMessage(error));
        return;
      }
      reportError(error, { title: "Could not save opening hours", onConflict: onReload });
    }
  };

  return (
    <Section
      title="Opening hours"
      actions={
        <Button onClick={() => void submit()} loading={save.isPending}>
          Save hours
        </Button>
      }
    >
      <FormAlert>{refusal}</FormAlert>
      <table className="w-full text-sm">
        <caption className="sr-only">Opening hours</caption>
        <thead>
          <tr className="text-text-muted text-left text-xs uppercase">
            <th className="py-1 font-medium">Day</th>
            <th className="py-1 font-medium">Open</th>
            <th className="py-1 font-medium">Opens</th>
            <th className="py-1 font-medium">Closes</th>
          </tr>
        </thead>
        <tbody>
          {WEEK.map(({ day, label }) => (
            <tr key={day} className="border-border border-t">
              <th scope="row" className="py-2 text-left font-medium">
                {label}
              </th>
              <td className="py-2">
                <Checkbox
                  aria-label={`${label} open`}
                  checked={rows[day].open}
                  onCheckedChange={(v) => setRow(day, { open: v === true })}
                />
              </td>
              <td className="py-2 pr-2">
                <Input
                  type="time"
                  step={900}
                  aria-label={`${label} opens at`}
                  disabled={!rows[day].open}
                  value={rows[day].opensAt}
                  onChange={(e) => setRow(day, { opensAt: e.target.value })}
                />
              </td>
              <td className="py-2">
                <Input
                  type="time"
                  step={900}
                  aria-label={`${label} closes at`}
                  disabled={!rows[day].open}
                  value={rows[day].closesAt}
                  onChange={(e) => setRow(day, { closesAt: e.target.value })}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Section>
  );
}

const closureSchema = z.object({
  closedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Choose the date the location is closed."),
  reason: z.string().trim().max(200),
});

function Closures({ locationId }: { locationId: string }) {
  const closures = useClosures(locationId);
  const add = useAddClosure(locationId);
  const remove = useRemoveClosure(locationId);
  const form = useForm<z.infer<typeof closureSchema>>({
    resolver: zodResolver(closureSchema),
    defaultValues: { closedOn: "", reason: "" },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      await add.mutateAsync({ closedOn: v.closedOn, reason: emptyToNull(v.reason) });
      notifySaved("Closure added", "Slots on that date close automatically.");
      form.reset();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["closedOn", "reason"],
        codeFields: { DUPLICATE: "closedOn" },
      });
    }
  });
  const items = closures.data ?? [];
  return (
    <Section title="Closures" description="Holidays and other days the location does not deliver.">
      <form noValidate onSubmit={onSubmit} className="flex flex-wrap items-end gap-3">
        <TextField
          form={form}
          name="closedOn"
          label="Date"
          type="date"
          min={todayInIndia()}
          required
          className="w-44"
        />
        <TextField form={form} name="reason" label="Reason" className="min-w-48 flex-1" />
        <Button type="submit" variant="outline" loading={add.isPending}>
          <Plus aria-hidden="true" />
          Add closure
        </Button>
      </form>
      {closures.isPending ? (
        <p className="text-text-muted text-sm">Loading closures…</p>
      ) : closures.error ? (
        <p role="alert" className="text-danger-text text-sm">
          {errorMessage(closures.error)}
        </p>
      ) : items.length === 0 ? (
        <p className="text-text-muted text-sm">No closures planned.</p>
      ) : (
        <ul className="divide-border flex flex-col divide-y">
          {items.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-2 py-2 text-sm">
              <span>
                <span className="font-medium">{formatCalendarDate(c.closedOn)}</span>
                {c.reason && <span className="text-text-muted"> · {c.reason}</span>}
              </span>
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Remove closure on ${formatCalendarDate(c.closedOn)}`}
                disabled={remove.isPending}
                onClick={() =>
                  remove.mutate(c.id, {
                    onSuccess: () => notifySaved("Closure removed"),
                    onError: (error) =>
                      reportError(error, { title: "Could not remove the closure" }),
                  })
                }
              >
                <Trash2 aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}

function LocationServiceAreas({ location }: { location: FulfillmentLocation }) {
  const areas = useServiceAreas({ fulfillmentLocationId: location.id, pageSize: 100 });
  const [creating, setCreating] = useState(false);
  return (
    <>
      <Section
        title="Service areas"
        actions={
          <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
            <Plus aria-hidden="true" />
            New service area
          </Button>
        }
      >
        <ServiceAreaList
          isPending={areas.isPending}
          error={areas.error}
          items={areas.data?.items ?? []}
        />
      </Section>
      {creating && (
        <CreateServiceAreaDialog
          cityId={location.cityId}
          locations={[{ value: location.id, label: location.name }]}
          onClose={() => setCreating(false)}
        />
      )}
    </>
  );
}
