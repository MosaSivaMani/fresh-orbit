import { formatDateTime, formatRupees, isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, EmptyState, StatusBadge } from "@fresh/ui";
import { CheckCircle2, Plus, XCircle } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router";
import { z } from "zod";
import {
  type CityTransition,
  useCity,
  useCityTransition,
  useFulfillmentLocations,
  useServiceAreas,
  useUpdateCity,
} from "../../api/locations";
import { type CityDetail, type Readiness } from "../../api/types/locations";
import { TextField } from "../../components/form";
import { Can, DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { ACTIVE_FLAG, CITY_STATUS, LOCATION_STATUS } from "../../lib/status";
import { CreateLocationDialog, CreateServiceAreaDialog } from "./dialogs";
import { FormAlert, READINESS_ITEMS, problemItems, reportWriteError } from "./shared";

export function CityDetailPage() {
  const { cityId = "" } = useParams();
  const city = useCity(cityId);
  return (
    <QueryView query={city} what="city">
      {(c) => <CityDetail city={c} onReload={() => void city.refetch()} />}
    </QueryView>
  );
}

function CityDetail({ city, onReload }: { city: CityDetail; onReload: () => void }) {
  const [editing, setEditing] = useState(false);
  const [transition, setTransition] = useState<CityTransition | null>(null);

  return (
    <>
      <PageHeader
        backTo="/cities"
        backLabel="Cities"
        title={city.name}
        meta={<StatusBadge code={city.status} map={CITY_STATUS} />}
        description={`${city.code} · ${city.stateCode} · ${city.timeZone}`}
        actions={
          <>
            <Button variant="outline" onClick={() => setEditing(true)}>
              Edit
            </Button>
            <Can perm="cities.activate">
              {(city.status === "DRAFT" || city.status === "PAUSED") && (
                <Button
                  onClick={() => setTransition("activate")}
                  disabled={!city.readiness.isReady}
                  title={city.readiness.isReady ? undefined : "Complete every launch check first."}
                >
                  Activate
                </Button>
              )}
              {city.status === "ACTIVE" && (
                <Button variant="outline" onClick={() => setTransition("pause")}>
                  Pause
                </Button>
              )}
              {city.status !== "RETIRED" && (
                <Button variant="danger" onClick={() => setTransition("retire")}>
                  Retire
                </Button>
              )}
            </Can>
          </>
        }
      />
      <div className="flex flex-col gap-6">
        <ReadinessChecklist cityId={city.id} readiness={city.readiness} />
        {city.activatedAt && (
          <Section title="Launch">
            <DetailList items={[{ label: "Activated", value: formatDateTime(city.activatedAt) }]} />
          </Section>
        )}
        <CityLocations cityId={city.id} />
        <CityServiceAreas cityId={city.id} />
      </div>
      {editing && (
        <EditCityDialog city={city} onClose={() => setEditing(false)} onReload={onReload} />
      )}
      {transition && (
        <TransitionDialog
          city={city}
          action={transition}
          onClose={() => setTransition(null)}
          onReload={onReload}
        />
      )}
    </>
  );
}

/** Every launch check with ✓ / ✗ and where to fix it; the server decides readiness (`isReady`). */
function ReadinessChecklist({ cityId, readiness }: { cityId: string; readiness: Readiness }) {
  return (
    <Section
      title="Launch checklist"
      description={
        readiness.isReady
          ? "Every check passes. The city can be activated."
          : "The city can be activated when every check passes."
      }
    >
      <ul aria-label="Launch checklist" className="divide-border flex flex-col divide-y">
        {READINESS_ITEMS.map((item) => {
          const missing = readiness.missing.filter((m) => m.code === item.code);
          const ok = missing.length === 0;
          const to = typeof item.fix.to === "function" ? item.fix.to(cityId) : item.fix.to;
          return (
            <li
              key={item.code}
              data-code={item.code}
              className="flex items-start justify-between gap-3 py-2.5"
            >
              <span className="flex items-start gap-2">
                {ok ? (
                  <CheckCircle2
                    aria-hidden="true"
                    className="text-success-text mt-0.5 size-5 shrink-0"
                  />
                ) : (
                  <XCircle aria-hidden="true" className="text-danger-text mt-0.5 size-5 shrink-0" />
                )}
                <span className="flex flex-col gap-0.5">
                  <span className="text-sm font-medium">
                    {item.label}
                    <span className="sr-only">{ok ? " — done" : " — missing"}</span>
                  </span>
                  {missing.map((m, i) => (
                    <span
                      key={`${m.code}-${m.fulfillmentLocationId ?? i}`}
                      className="text-text-muted text-sm"
                    >
                      {m.message}
                    </span>
                  ))}
                </span>
              </span>
              {!ok && (
                <Link
                  to={to}
                  className="text-accent-text shrink-0 text-sm font-medium hover:underline"
                >
                  {item.fix.label}
                </Link>
              )}
            </li>
          );
        })}
      </ul>
      {/* Codes the checklist does not know yet still show, so nothing the server reports is hidden. */}
      {readiness.missing
        .filter((m) => !READINESS_ITEMS.some((i) => i.code === m.code))
        .map((m, i) => (
          <p key={`${m.code}-${i}`} className="text-danger-text text-sm">
            {m.message}
          </p>
        ))}
    </Section>
  );
}

function CityLocations({ cityId }: { cityId: string }) {
  const locations = useFulfillmentLocations({ cityId, pageSize: 100 });
  const [creating, setCreating] = useState(false);
  const items = locations.data?.items ?? [];
  return (
    <div id="locations">
      <Section
        title="Fulfillment locations"
        actions={
          <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
            <Plus aria-hidden="true" />
            New location
          </Button>
        }
      >
        {locations.isPending ? (
          <p className="text-text-muted text-sm">Loading locations…</p>
        ) : locations.error ? (
          <p role="alert" className="text-danger-text text-sm">
            {errorMessage(locations.error)}
          </p>
        ) : items.length === 0 ? (
          <EmptyState
            title="No locations yet"
            description="Add the store or dark store that serves this city."
          />
        ) : (
          <ul className="divide-border flex flex-col divide-y">
            {items.map((l) => (
              <li key={l.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <span className="flex flex-col">
                  <Link
                    to={`/locations/${l.id}`}
                    className="text-accent-text font-medium hover:underline"
                  >
                    {l.name}
                  </Link>
                  <span className="text-text-muted text-sm">
                    {l.code}
                    {l.hasColdStorage ? " · cold storage" : ""}
                  </span>
                </span>
                <StatusBadge code={l.status} map={LOCATION_STATUS} />
              </li>
            ))}
          </ul>
        )}
      </Section>
      {creating && <CreateLocationDialog cityId={cityId} onClose={() => setCreating(false)} />}
    </div>
  );
}

function CityServiceAreas({ cityId }: { cityId: string }) {
  const areas = useServiceAreas({ cityId, pageSize: 100 });
  const locations = useFulfillmentLocations({ cityId, pageSize: 100 });
  const [creating, setCreating] = useState(false);
  const names = new Map((locations.data?.items ?? []).map((l) => [l.id, l.name]));
  const items = areas.data?.items ?? [];
  return (
    <div id="service-areas">
      <Section
        title="Service areas"
        description="Where each location delivers, with its fees and pincodes."
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
          items={items}
          locationName={(id) => names.get(id)}
        />
      </Section>
      {creating && (
        <CreateServiceAreaDialog
          cityId={cityId}
          locations={(locations.data?.items ?? []).map((l) => ({ value: l.id, label: l.name }))}
          onClose={() => setCreating(false)}
        />
      )}
    </div>
  );
}

export function ServiceAreaList({
  isPending,
  error,
  items,
  locationName,
}: {
  isPending: boolean;
  error: unknown;
  items: readonly {
    id: string;
    name: string;
    fulfillmentLocationId: string;
    deliveryFeePaise: number;
    freeDeliveryAbovePaise: number | null;
    minOrderPaise: number;
    isActive: boolean;
    activePincodeCount: number;
  }[];
  locationName?: (id: string) => string | undefined;
}) {
  if (isPending) return <p className="text-text-muted text-sm">Loading service areas…</p>;
  if (error)
    return (
      <p role="alert" className="text-danger-text text-sm">
        {errorMessage(error)}
      </p>
    );
  if (items.length === 0)
    return (
      <EmptyState title="No service areas yet" description="Add the area a location delivers to." />
    );
  return (
    <ul className="divide-border flex flex-col divide-y">
      {items.map((a) => (
        <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
          <span className="flex flex-col">
            <Link
              to={`/service-areas/${a.id}`}
              className="text-accent-text font-medium hover:underline"
            >
              {a.name}
            </Link>
            <span className="text-text-muted text-sm">
              {locationName?.(a.fulfillmentLocationId)
                ? `${locationName(a.fulfillmentLocationId)} · `
                : ""}
              Fee {formatRupees(a.deliveryFeePaise)}
              {a.freeDeliveryAbovePaise != null
                ? `, free above ${formatRupees(a.freeDeliveryAbovePaise)}`
                : ""}{" "}
              · min order {formatRupees(a.minOrderPaise)} · {a.activePincodeCount} pincodes
            </span>
          </span>
          <StatusBadge code={String(a.isActive)} map={ACTIVE_FLAG} />
        </li>
      ))}
    </ul>
  );
}

const editCitySchema = z.object({
  name: z.string().trim().min(1, "Enter the city name.").max(120),
  stateCode: z
    .string()
    .trim()
    .regex(/^[A-Z]{2}$/, "Enter the two-letter state code, e.g. TG."),
  timeZone: z.string().trim().min(1, "Enter the IANA time zone, e.g. Asia/Kolkata."),
});

function EditCityDialog({
  city,
  onClose,
  onReload,
}: {
  city: CityDetail;
  onClose: () => void;
  onReload: () => void;
}) {
  const update = useUpdateCity(city.id);
  const form = useForm<z.infer<typeof editCitySchema>>({
    resolver: zodResolver(editCitySchema),
    defaultValues: { name: city.name, stateCode: city.stateCode, timeZone: city.timeZone },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await update.mutateAsync({ body: values, version: city.version });
      notifySaved("City saved");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["name", "stateCode", "timeZone"],
        onConflict: () => {
          onClose();
          onReload();
        },
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title={`Edit ${city.name}`}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="edit-city" loading={update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <form id="edit-city" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <TextField form={form} name="name" label="Name" required />
          <TextField form={form} name="stateCode" label="State code" required />
          <TextField form={form} name="timeZone" label="Time zone" required />
        </form>
      </DialogContent>
    </Dialog>
  );
}

const TRANSITION_COPY: Record<
  CityTransition,
  { title: string; body: string; button: string; done: string }
> = {
  activate: {
    title: "Activate this city?",
    body: "Customers in its service areas can see the catalogue and place orders as soon as it is active.",
    button: "Activate",
    done: "City activated",
  },
  pause: {
    title: "Pause this city?",
    body: "New orders stop. Orders already placed are still packed and delivered.",
    button: "Pause",
    done: "City paused",
  },
  retire: {
    title: "Retire this city?",
    body: "The city stops serving customers. Retiring is refused while it still has open orders.",
    button: "Retire",
    done: "City retired",
  },
};

function TransitionDialog({
  city,
  action,
  onClose,
  onReload,
}: {
  city: CityDetail;
  action: CityTransition;
  onClose: () => void;
  onReload: () => void;
}) {
  const run = useCityTransition(city.id);
  const [refusal, setRefusal] = useState<{ message: string; items: string[] } | null>(null);
  const copy = TRANSITION_COPY[action];

  const confirm = async () => {
    setRefusal(null);
    try {
      await run.mutateAsync({ action, version: city.version });
      notifySaved(copy.done);
      onClose();
    } catch (error) {
      if (isApiError(error) && error.code === "CITY_NOT_READY") {
        setRefusal({ message: errorMessage(error), items: problemItems(error, "missing") });
        return;
      }
      if (isApiError(error) && error.code === "CITY_RETIREMENT_BLOCKED") {
        setRefusal({ message: errorMessage(error), items: problemItems(error, "blocking") });
        return;
      }
      reportError(error, {
        title: `Could not ${action} the city`,
        onConflict: () => {
          onClose();
          onReload();
        },
      });
    }
  };

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title={copy.title}
        description={copy.body}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              variant={action === "retire" ? "danger" : "primary"}
              loading={run.isPending}
              onClick={() => void confirm()}
            >
              {copy.button}
            </Button>
          </>
        }
      >
        {refusal && (
          <FormAlert>
            <p>{refusal.message}</p>
            {refusal.items.length > 0 && (
              <ul aria-label="What is missing" className="mt-1 list-disc pl-5">
                {refusal.items.map((item, i) => (
                  <li key={`${i}-${item}`}>{item}</li>
                ))}
              </ul>
            )}
          </FormAlert>
        )}
      </DialogContent>
    </Dialog>
  );
}
