import { zodResolver } from "@hookform/resolvers/zod";
import { formatPercentFromBp, formatRupees, humanizeCode } from "@fresh/core";
import {
  Badge,
  Button,
  Checkbox,
  Combobox,
  Dialog,
  DialogContent,
  EmptyState,
  FormField,
} from "@fresh/ui";
import { FileSliders, Plus } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { useCityLookup } from "../../api/locations";
import { useOrderPolicies, useSaveOrderPolicy } from "../../api/settings";
import {
  type CancelAllowedUntilStatus,
  type OrderPolicy,
  type PolicyPaymentMethod,
} from "../../api/types/settings";
import { SelectField, TextField } from "../../components/form";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import {
  bpToPercentInput,
  paiseToRupeesInput,
  parsePercentToBp,
  parseRupeesToPaise,
  parseWholeNumber,
} from "../../lib/units";

const CANCEL_UNTIL: { value: CancelAllowedUntilStatus; label: string }[] = [
  { value: "PENDING_PAYMENT", label: "Until payment" },
  { value: "CONFIRMED", label: "Until confirmed" },
  { value: "PREPARING", label: "Until preparation starts" },
];
const PAYMENT_METHODS: { value: PolicyPaymentMethod; label: string }[] = [
  { value: "COD", label: "Cash on delivery" },
  { value: "UPI_ON_DELIVERY", label: "UPI on delivery" },
  { value: "ONLINE", label: "Online" },
];

/** The policy being edited: an existing one, or a new default / city policy (`version: null`). */
type Target = { key: string; title: string; policy: OrderPolicy | null };

export function OrderPoliciesPage() {
  const policies = useOrderPolicies();
  const cities = useCityLookup();
  const [target, setTarget] = useState<Target | null>(null);
  const [addingCity, setAddingCity] = useState(false);
  const cityName = (id: string | null) =>
    cities.data?.items.find((c) => c.id === id)?.name ?? `City ${id?.slice(-6) ?? ""}`;

  return (
    <>
      <PageHeader
        title="Order policies"
        description="Rules the server applies at checkout and after. A city policy overrides the default."
        actions={
          <Button
            size="sm"
            variant="outline"
            leadingIcon={<Plus aria-hidden="true" />}
            onClick={() => setAddingCity(true)}
          >
            City policy
          </Button>
        }
      />
      <QueryView query={policies} what="order policies">
        {(items) => {
          const fallback = items.find((p) => p.isDefault) ?? null;
          const perCity = items.filter((p) => !p.isDefault);
          return (
            <div className="flex flex-col gap-4">
              {fallback ? (
                <PolicyCard
                  title="Default"
                  policy={fallback}
                  onEdit={() => setTarget({ key: "default", title: "Default", policy: fallback })}
                />
              ) : (
                <EmptyState
                  icon={<FileSliders aria-hidden="true" />}
                  title="No default policy"
                  description="Checkout is refused in cities without a policy until one is created."
                  action={
                    <Button
                      size="sm"
                      onClick={() => setTarget({ key: "default", title: "Default", policy: null })}
                    >
                      Create default policy
                    </Button>
                  }
                />
              )}
              {perCity.map((p) => (
                <PolicyCard
                  key={p.id}
                  title={cityName(p.cityId)}
                  policy={p}
                  onEdit={() =>
                    setTarget({ key: p.cityId ?? "default", title: cityName(p.cityId), policy: p })
                  }
                />
              ))}
              {addingCity && (
                <ChooseCityDialog
                  cities={(cities.data?.items ?? []).filter(
                    (c) => !perCity.some((p) => p.cityId === c.id),
                  )}
                  onClose={() => setAddingCity(false)}
                  onChoose={(id) => {
                    setAddingCity(false);
                    setTarget({ key: id, title: cityName(id), policy: null });
                  }}
                />
              )}
              {target && (
                <PolicyDialog
                  target={target}
                  template={fallback}
                  onClose={() => setTarget(null)}
                  onConflict={() => {
                    setTarget(null);
                    void policies.refetch();
                  }}
                />
              )}
            </div>
          );
        }}
      </QueryView>
    </>
  );
}

function PolicyCard({
  title,
  policy,
  onEdit,
}: {
  title: string;
  policy: OrderPolicy;
  onEdit: () => void;
}) {
  return (
    <Section
      title={title}
      actions={
        <>
          {policy.isDefault && <Badge tone="info">Default</Badge>}
          <Button size="sm" variant="outline" aria-label={`Edit ${title} policy`} onClick={onEdit}>
            Edit
          </Button>
        </>
      }
    >
      <DetailList
        columns={3}
        items={[
          { label: "Stock reservation", value: `${policy.reservationMinutes} min` },
          { label: "Max overweight", value: formatPercentFromBp(policy.maxOverweightBp) },
          {
            label: "Customer cancellation",
            value:
              CANCEL_UNTIL.find((c) => c.value === policy.cancelAllowedUntilStatus)?.label ??
              humanizeCode(policy.cancelAllowedUntilStatus),
          },
          { label: "COD order limit", value: formatRupees(policy.codMaxOrderPaise) },
          {
            label: "Payment methods",
            value: policy.enabledPaymentMethods
              .map((m) => PAYMENT_METHODS.find((p) => p.value === m)?.label ?? humanizeCode(m))
              .join(", "),
          },
          {
            label: "Substitution reply window",
            value: `${policy.substitutionResponseMinutes} min`,
          },
          { label: "Complaint window", value: `${policy.complaintWindowHours} h` },
          { label: "Delivery attempts", value: policy.maxDeliveryAttempts },
        ]}
      />
    </Section>
  );
}

function ChooseCityDialog({
  cities,
  onChoose,
  onClose,
}: {
  cities: { id: string; name: string }[];
  onChoose: (id: string) => void;
  onClose: () => void;
}) {
  const [cityId, setCityId] = useState<string | null>(null);
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title="New city policy"
        description="Starts from the default policy's values."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button disabled={!cityId} onClick={() => cityId && onChoose(cityId)}>
              Continue
            </Button>
          </>
        }
      >
        <FormField label="City">
          <Combobox
            options={cities.map((c) => ({ value: c.id, label: c.name }))}
            value={cityId}
            onValueChange={setCityId}
            placeholder="Choose a city"
            emptyText="Every city already has a policy."
          />
        </FormField>
      </DialogContent>
    </Dialog>
  );
}

const whole = (message: string) => z.string().refine((v) => parseWholeNumber(v) !== null, message);

const schema = z.object({
  reservationMinutes: whole("Enter the reservation time in whole minutes."),
  maxOverweightPercent: z
    .string()
    .refine((v) => parsePercentToBp(v) !== null, "Enter a percentage such as 10 or 7.5."),
  cancelAllowedUntilStatus: z.enum(["PENDING_PAYMENT", "CONFIRMED", "PREPARING"], {
    message: "Choose until when customers may cancel.",
  }),
  codMaxOrderRupees: z
    .string()
    .refine((v) => parseRupeesToPaise(v) !== null, "Enter an amount in rupees, e.g. 3000."),
  enabledPaymentMethods: z
    .array(z.enum(["COD", "UPI_ON_DELIVERY", "ONLINE"]))
    .min(1, "Enable at least one payment method."),
  substitutionResponseMinutes: whole("Enter the reply window in whole minutes."),
  complaintWindowHours: whole("Enter the complaint window in whole hours."),
  maxDeliveryAttempts: whole("Enter the number of delivery attempts."),
});
type Values = z.infer<typeof schema>;
const FIELDS = Object.keys(schema.shape);
const ALIASES = { maxOverweightBp: "maxOverweightPercent", codMaxOrderPaise: "codMaxOrderRupees" };

function PolicyDialog({
  target,
  template,
  onClose,
  onConflict,
}: {
  target: Target;
  template: OrderPolicy | null;
  onClose: () => void;
  onConflict: () => void;
}) {
  const save = useSaveOrderPolicy();
  const source = target.policy ?? template;
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      reservationMinutes: source ? String(source.reservationMinutes) : "",
      maxOverweightPercent: bpToPercentInput(source?.maxOverweightBp),
      cancelAllowedUntilStatus: source?.cancelAllowedUntilStatus ?? "CONFIRMED",
      codMaxOrderRupees: paiseToRupeesInput(source?.codMaxOrderPaise),
      enabledPaymentMethods: source?.enabledPaymentMethods ?? [],
      substitutionResponseMinutes: source ? String(source.substitutionResponseMinutes) : "",
      complaintWindowHours: source ? String(source.complaintWindowHours) : "",
      maxDeliveryAttempts: source ? String(source.maxDeliveryAttempts) : "",
    },
  });

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(
      {
        key: target.key,
        version: target.policy?.version ?? null,
        body: {
          reservationMinutes: parseWholeNumber(v.reservationMinutes)!,
          maxOverweightBp: parsePercentToBp(v.maxOverweightPercent)!,
          cancelAllowedUntilStatus: v.cancelAllowedUntilStatus,
          codMaxOrderPaise: parseRupeesToPaise(v.codMaxOrderRupees)!,
          enabledPaymentMethods: v.enabledPaymentMethods,
          substitutionResponseMinutes: parseWholeNumber(v.substitutionResponseMinutes)!,
          complaintWindowHours: parseWholeNumber(v.complaintWindowHours)!,
          maxDeliveryAttempts: parseWholeNumber(v.maxDeliveryAttempts)!,
        },
      },
      {
        onSuccess: () => {
          notifySaved(`${target.title} policy saved`);
          onClose();
        },
        onError: (error) =>
          reportError(error, {
            setError: form.setError,
            fields: FIELDS,
            aliases: ALIASES,
            onConflict,
          }),
      },
    ),
  );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title={`${target.policy ? "Edit" : "Create"} ${target.title} policy`}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={save.isPending} onClick={() => void onSubmit()}>
              Save policy
            </Button>
          </>
        }
      >
        <form
          className="grid grid-cols-1 gap-4 sm:grid-cols-2"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit();
          }}
        >
          <TextField
            form={form}
            name="reservationMinutes"
            label="Stock reservation (minutes)"
            inputMode="numeric"
            required
          />
          <TextField
            form={form}
            name="maxOverweightPercent"
            label="Max overweight (%)"
            inputMode="decimal"
            required
          />
          <SelectField
            form={form}
            name="cancelAllowedUntilStatus"
            label="Customers may cancel"
            options={CANCEL_UNTIL}
            required
          />
          <TextField
            form={form}
            name="codMaxOrderRupees"
            label="COD order limit (₹)"
            inputMode="decimal"
            required
          />
          <TextField
            form={form}
            name="substitutionResponseMinutes"
            label="Substitution reply window (minutes)"
            inputMode="numeric"
            required
          />
          <TextField
            form={form}
            name="complaintWindowHours"
            label="Complaint window (hours)"
            inputMode="numeric"
            required
          />
          <TextField
            form={form}
            name="maxDeliveryAttempts"
            label="Delivery attempts"
            inputMode="numeric"
            required
          />
          <FormField
            label="Payment methods"
            required
            error={form.formState.errors.enabledPaymentMethods?.message}
            className="sm:col-span-2"
          >
            <Controller
              control={form.control}
              name="enabledPaymentMethods"
              render={({ field }) => (
                <div className="flex flex-wrap gap-4">
                  {PAYMENT_METHODS.map((m) => (
                    <Checkbox
                      key={m.value}
                      label={m.label}
                      checked={field.value.includes(m.value)}
                      onCheckedChange={(checked) =>
                        field.onChange(
                          checked === true
                            ? [...field.value, m.value]
                            : field.value.filter((x) => x !== m.value),
                        )
                      }
                    />
                  ))}
                </div>
              )}
            />
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  );
}
