import { formatTimeRange, isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Button,
  Dialog,
  DialogContent,
  EmptyState,
  ErrorState,
  Skeleton,
  StatusBadge,
} from "@fresh/ui";
import { CalendarClock, Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useCreateSlotTemplate, useSlotTemplates, useUpdateSlotTemplate } from "../../api/delivery";
import { type SlotTemplate } from "../../api/types/delivery";
import { useLocationScope } from "../../app/location-scope";
import { SelectField, SwitchField, TextField } from "../../components/form";
import { LocationRequired, PageHeader, Section } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved } from "../../lib/feedback";
import { ACTIVE_FLAG } from "../../lib/status";
import { parseWholeNumber } from "../../lib/units";
import { DAYS, DeliveryNav, FormAlert, dayLabel, reportWriteError } from "./shared";

/** Weekly slot templates of one location; nightly generation turns them into dated slots. */
export function TemplatesPage() {
  const scope = useLocationScope();
  const locationId = scope.locationId;
  const templates = useSlotTemplates(
    { fulfillmentLocationId: locationId ?? "", pageSize: 100 },
    Boolean(locationId),
  );
  const [editing, setEditing] = useState<SlotTemplate | "new" | null>(null);

  const header = (
    <>
      <PageHeader
        title="Delivery"
        description={`Slot templates · ${scope.nameOf(locationId)}`}
        actions={
          locationId && (
            <Button onClick={() => setEditing("new")}>
              <Plus aria-hidden="true" />
              New template
            </Button>
          )
        }
      />
      <DeliveryNav />
    </>
  );

  if (!locationId) {
    return (
      <>
        {header}
        <LocationRequired action="manage its slot templates" />
      </>
    );
  }

  const items = templates.data?.items ?? [];
  return (
    <>
      {header}
      {templates.isPending ? (
        <div aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : templates.error ? (
        <ErrorState
          title="Could not load slot templates"
          traceId={isApiError(templates.error) ? templates.error.traceId : undefined}
          onRetry={() => void templates.refetch()}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={<CalendarClock aria-hidden="true" />}
          title="No slot templates yet"
          description="Add the weekly delivery windows this location offers. A city cannot be activated without them."
          action={<Button onClick={() => setEditing("new")}>New template</Button>}
        />
      ) : (
        <div className="flex flex-col gap-4">
          {DAYS.map((day) => {
            const ofDay = items
              .filter((t) => String(t.dayOfWeek) === day.value)
              .sort((a, b) => a.startTime.localeCompare(b.startTime));
            if (ofDay.length === 0) return null;
            return (
              <Section key={day.value} title={day.label}>
                <ul className="divide-border flex flex-col divide-y">
                  {ofDay.map((t) => (
                    <li
                      key={t.id}
                      className="flex flex-wrap items-center justify-between gap-3 py-2"
                    >
                      <span className="flex flex-col">
                        <span className="font-medium">
                          {formatTimeRange(t.startTime, t.endTime)}
                        </span>
                        <span className="text-text-muted text-sm">
                          {t.capacity} orders · cut-off {t.cutoffMinutes} min before
                        </span>
                      </span>
                      <span className="flex items-center gap-2">
                        <StatusBadge code={String(t.isActive)} map={ACTIVE_FLAG} />
                        <Button
                          size="sm"
                          variant="outline"
                          aria-label={`Edit ${day.label} ${formatTimeRange(t.startTime, t.endTime)}`}
                          onClick={() => setEditing(t)}
                        >
                          Edit
                        </Button>
                      </span>
                    </li>
                  ))}
                </ul>
              </Section>
            );
          })}
        </div>
      )}
      {editing && (
        <TemplateDialog
          locationId={locationId}
          template={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </>
  );
}

const TIME = /^\d{2}:\d{2}$/;

const templateSchema = z.object({
  dayOfWeek: z.string().min(1, "Choose the day."),
  startTime: z.string().regex(TIME, "Enter the start time."),
  endTime: z.string().regex(TIME, "Enter the end time."),
  capacity: z
    .string()
    .refine((v) => (parseWholeNumber(v) ?? 0) > 0, "Enter capacity as a whole number above 0."),
  cutoffMinutes: z
    .string()
    .refine((v) => parseWholeNumber(v) !== null, "Enter the cut-off in whole minutes."),
  isActive: z.boolean(),
});

type TemplateForm = z.infer<typeof templateSchema>;

function TemplateDialog({
  locationId,
  template,
  onClose,
}: {
  locationId: string;
  template: SlotTemplate | null;
  onClose: () => void;
}) {
  const create = useCreateSlotTemplate();
  const update = useUpdateSlotTemplate();
  const [refusal, setRefusal] = useState<string | null>(null);
  const form = useForm<TemplateForm>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      dayOfWeek: template ? String(template.dayOfWeek) : "",
      startTime: template?.startTime.slice(0, 5) ?? "",
      endTime: template?.endTime.slice(0, 5) ?? "",
      capacity: template ? String(template.capacity) : "",
      cutoffMinutes: template ? String(template.cutoffMinutes) : "",
      isActive: template?.isActive ?? true,
    },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    setRefusal(null);
    const body = {
      dayOfWeek: Number(values.dayOfWeek),
      startTime: values.startTime,
      endTime: values.endTime,
      capacity: parseWholeNumber(values.capacity) ?? 0,
      cutoffMinutes: parseWholeNumber(values.cutoffMinutes) ?? 0,
    };
    try {
      if (template) {
        await update.mutateAsync({
          id: template.id,
          version: template.version,
          body: { ...body, isActive: values.isActive },
        });
      } else {
        await create.mutateAsync({ ...body, fulfillmentLocationId: locationId });
      }
      notifySaved(template ? "Slot template saved" : "Slot template added");
      onClose();
    } catch (error) {
      if (isApiError(error) && error.code === "SLOT_TEMPLATE_OVERLAP") {
        setRefusal(errorMessage(error));
        return;
      }
      reportWriteError(error, {
        setError: form.setError,
        fields: ["dayOfWeek", "startTime", "endTime", "capacity", "cutoffMinutes", "isActive"],
        onConflict: onClose,
      });
    }
  });
  const title = template
    ? `Edit ${dayLabel(template.dayOfWeek)} ${formatTimeRange(template.startTime, template.endTime)}`
    : "New slot template";
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        title={title}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="slot-template"
              loading={create.isPending || update.isPending}
            >
              Save
            </Button>
          </>
        }
      >
        <form id="slot-template" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <FormAlert>{refusal}</FormAlert>
          <SelectField form={form} name="dayOfWeek" label="Day" required options={DAYS} />
          <div className="grid grid-cols-2 gap-4">
            <TextField
              form={form}
              name="startTime"
              label="Starts"
              type="time"
              step={900}
              required
            />
            <TextField form={form} name="endTime" label="Ends" type="time" step={900} required />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <TextField
              form={form}
              name="capacity"
              label="Capacity (orders)"
              inputMode="numeric"
              required
            />
            <TextField
              form={form}
              name="cutoffMinutes"
              label="Cut-off (minutes before start)"
              inputMode="numeric"
              required
            />
          </div>
          {template && (
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive templates stop generating new slots."
            />
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}
