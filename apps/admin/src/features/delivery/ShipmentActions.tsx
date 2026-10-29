import { formatRupees, isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, toast } from "@fresh/ui";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import {
  useAssignRider,
  useDeliverShipment,
  useDispatchShipment,
  useReattempt,
  useRecordAttempt,
  useRecordCashCollected,
  useReloadShipment,
  useReturnShipment,
  useRiders,
} from "../../api/delivery";
import { type Shipment, type ShipmentStatus } from "../../api/types/delivery";
import { SelectField, TextAreaField, TextField } from "../../components/form";
import { notifySaved, reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import { SHIPMENT_STATUS } from "../../lib/status";
import { paiseToRupeesInput, parseRupeesToPaise } from "../../lib/units";
import { paymentMethodLabel, reportWriteError } from "./shared";

type DialogKind = "assign" | "dispatch" | "deliver" | "attempt" | "cash" | "reattempt" | "return";

const CASH_MODES = [
  { value: "CASH", label: "Cash" },
  { value: "UPI", label: "UPI" },
];

const OUTCOMES = [
  { value: "CUSTOMER_UNAVAILABLE", label: "Customer unavailable" },
  { value: "ADDRESS_NOT_FOUND", label: "Address not found" },
  { value: "REFUSED", label: "Refused" },
  { value: "OTHER", label: "Other" },
];

/** The statuses each action is offered in; a reload after a 412 repeats the action only while it still applies. */
const ALLOWED: Record<Exclude<DialogKind, "cash">, readonly ShipmentStatus[]> = {
  assign: ["PENDING", "ASSIGNED"],
  dispatch: ["ASSIGNED"],
  deliver: ["OUT_FOR_DELIVERY"],
  attempt: ["OUT_FOR_DELIVERY"],
  reattempt: ["FAILED"],
  return: ["FAILED"],
};

const optionalRupees = z
  .string()
  .trim()
  .refine(
    (v) => v === "" || parseRupeesToPaise(v) !== null,
    "Enter an amount in rupees, e.g. 450 or 450.50.",
  );

/** True when the server says the rider collects nothing (prepaid order, AD-2). */
function isPrepaid(shipment: Shipment): boolean {
  return shipment.amountToCollectPaise === 0;
}

/**
 * The actions a staff member can take on a shipment in its current status (docs/api §4.17). The server still
 * decides: a refused transition comes back as a Problem Details code and is shown in place.
 */
export function ShipmentActions({
  shipment,
  size = "sm",
}: {
  shipment: Shipment;
  size?: "sm" | "md";
}) {
  const [open, setOpen] = useState<DialogKind | null>(null);
  const close = () => setOpen(null);
  const s = shipment.status;
  const cashOpen =
    shipment.cashCollectedPaise == null &&
    !isPrepaid(shipment) &&
    (s === "OUT_FOR_DELIVERY" || s === "DELIVERED");

  return (
    <div className="flex flex-wrap gap-1.5">
      {(s === "PENDING" || s === "ASSIGNED") && (
        <Button size={size} variant="outline" onClick={() => setOpen("assign")}>
          {shipment.riderId ? "Change rider" : "Assign rider"}
        </Button>
      )}
      {s === "ASSIGNED" && (
        <Button size={size} onClick={() => setOpen("dispatch")}>
          Dispatch
        </Button>
      )}
      {s === "OUT_FOR_DELIVERY" && (
        <>
          <Button size={size} onClick={() => setOpen("deliver")}>
            Deliver
          </Button>
          <Button size={size} variant="outline" onClick={() => setOpen("attempt")}>
            Failed attempt
          </Button>
        </>
      )}
      {s === "FAILED" && (
        <>
          <Button size={size} variant="outline" onClick={() => setOpen("reattempt")}>
            Reattempt
          </Button>
          <Button size={size} variant="outline" onClick={() => setOpen("return")}>
            Return
          </Button>
        </>
      )}
      {cashOpen && (
        <Button size={size} variant="ghost" onClick={() => setOpen("cash")}>
          Record cash
        </Button>
      )}

      {open === "assign" && <AssignDialog shipment={shipment} onClose={close} />}
      {open === "dispatch" && <DispatchDialog shipment={shipment} onClose={close} />}
      {open === "deliver" && <DeliverDialog shipment={shipment} onClose={close} />}
      {open === "attempt" && <AttemptDialog shipment={shipment} onClose={close} />}
      {open === "cash" && <CashDialog shipment={shipment} onClose={close} />}
      {(open === "reattempt" || open === "return") && (
        <ConfirmTransition shipment={shipment} kind={open} onClose={close} />
      )}
    </div>
  );
}

interface DialogProps {
  shipment: Shipment;
  onClose: () => void;
}

/**
 * AD-10: the "Reload" of the 412 prompt re-reads the shipment (and refreshes the board), then repeats the same
 * action with the new version while the shipment is still in a status that offers it. Otherwise the dialog closes
 * and says what the shipment is now; nothing is repeated.
 */
function useRetryAfterConflict(
  shipment: Shipment,
  kind: keyof typeof ALLOWED,
  onClose: () => void,
) {
  const reload = useReloadShipment();
  return (retry: (version: string) => Promise<void>) => async () => {
    let fresh: Shipment;
    try {
      fresh = await reload(shipment.id);
    } catch (error) {
      reportError(error, { title: "Could not reload the shipment" });
      return;
    }
    if (ALLOWED[kind].includes(fresh.status)) {
      await retry(fresh.version);
      return;
    }
    toast({
      title: "This shipment changed",
      description: `It is now ${SHIPMENT_STATUS[fresh.status]?.label ?? fresh.status}. Nothing was repeated.`,
      tone: "warning",
    });
    onClose();
  };
}

/** Who and where (AD-1) and what to collect (AD-2), at the top of each dialog. */
function ShipmentSummary({ shipment }: { shipment: Shipment }) {
  if (!shipment.orderNumber) return null;
  return (
    <div className="bg-surface-sand rounded-xl px-3 py-2 text-sm">
      <p className="font-semibold">
        Order {shipment.orderNumber} · {shipment.recipientName}
      </p>
      {shipment.addressLine && (
        <p className="text-text-muted">
          {shipment.addressLine}
          {shipment.pincode ? ` ${shipment.pincode}` : ""}
        </p>
      )}
      <CollectLine shipment={shipment} />
    </div>
  );
}

/** "Collect ₹669.50 · Cash on delivery" or "Prepaid: nothing to collect", from the server's amount (AD-2). */
export function CollectLine({ shipment }: { shipment: Shipment }) {
  if (shipment.amountToCollectPaise == null) return null;
  if (isPrepaid(shipment)) {
    return <p className="text-text-muted">Prepaid: nothing to collect</p>;
  }
  return (
    <p className="font-semibold">
      Collect {formatRupees(shipment.amountToCollectPaise)}
      {shipment.paymentMethod ? ` · ${paymentMethodLabel(shipment.paymentMethod)}` : ""}
    </p>
  );
}

const assignSchema = z.object({ riderId: z.string().min(1, "Choose a rider.") });

function AssignDialog({ shipment, onClose }: DialogProps) {
  const riders = useRiders({
    fulfillmentLocationId: shipment.fulfillmentLocationId,
    isActive: true,
    pageSize: 100,
  });
  const assign = useAssignRider();
  const retryAfterConflict = useRetryAfterConflict(shipment, "assign", onClose);
  const form = useForm<z.infer<typeof assignSchema>>({
    resolver: zodResolver(assignSchema),
    defaultValues: { riderId: shipment.riderId ?? "" },
  });
  async function send(values: z.infer<typeof assignSchema>, version: string): Promise<void> {
    try {
      await assign.mutateAsync({ id: shipment.id, version, body: values });
      notifySaved("Rider assigned");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["riderId"],
        codeFields: { RIDER_NOT_AVAILABLE: "riderId" },
        onConflict: retryAfterConflict((next) => send(values, next)),
      });
    }
  }
  const onSubmit = form.handleSubmit((values) => send(values, shipment.version));
  const options = (riders.data?.items ?? []).map((r) => ({
    value: r.id,
    label: `${r.fullName} (${r.phone})`,
  }));
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title={shipment.riderId ? "Change rider" : "Assign rider"}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="assign-rider" loading={assign.isPending}>
              Assign
            </Button>
          </>
        }
      >
        <form id="assign-rider" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <ShipmentSummary shipment={shipment} />
          <SelectField
            form={form}
            name="riderId"
            label="Rider"
            required
            options={options}
            placeholder={
              riders.isPending
                ? "Loading riders…"
                : options.length === 0
                  ? "No active riders"
                  : "Choose a rider"
            }
          />
        </form>
      </DialogContent>
    </Dialog>
  );
}

const dispatchSchema = z.object({
  dispatchTemperatureC: z
    .string()
    .trim()
    .min(1, "Enter the cold-box temperature.")
    .refine(
      (v) => /^-?\d{1,2}(\.\d)?$/.test(v),
      "Enter the temperature in °C with at most one decimal, e.g. 2.5.",
    ),
});

function DispatchDialog({ shipment, onClose }: DialogProps) {
  const dispatch = useDispatchShipment();
  const retryAfterConflict = useRetryAfterConflict(shipment, "dispatch", onClose);
  const form = useForm<z.infer<typeof dispatchSchema>>({
    resolver: zodResolver(dispatchSchema),
    defaultValues: { dispatchTemperatureC: "" },
  });
  async function send(values: z.infer<typeof dispatchSchema>, version: string): Promise<void> {
    try {
      const result = await dispatch.mutateAsync({
        id: shipment.id,
        version,
        body: { dispatchTemperatureC: Number(values.dispatchTemperatureC) },
      });
      notifySaved(
        "Shipment dispatched",
        result.temperatureWarning
          ? "Temperature is above the cold-chain limit; it was recorded with a warning."
          : undefined,
      );
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["dispatchTemperatureC"],
        title: "Could not dispatch",
        onConflict: retryAfterConflict((next) => send(values, next)),
      });
    }
  }
  const onSubmit = form.handleSubmit((values) => send(values, shipment.version));
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="Dispatch shipment"
        description="The order goes out for delivery with the assigned rider."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="dispatch-shipment" loading={dispatch.isPending}>
              Dispatch
            </Button>
          </>
        }
      >
        <form id="dispatch-shipment" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <ShipmentSummary shipment={shipment} />
          <TextField
            form={form}
            name="dispatchTemperatureC"
            label="Cold-box temperature (°C)"
            hint="Measured at dispatch. Above 4.0 °C is accepted and flagged."
            inputMode="decimal"
            required
          />
        </form>
      </DialogContent>
    </Dialog>
  );
}

const deliverSchema = z
  .object({
    deliveryCode: z
      .string()
      .trim()
      .regex(/^\d{4}$/, "Enter the 4-digit code the customer reads out."),
    cashCollected: optionalRupees,
    cashMode: z.string(),
    upiReference: z.string().trim().max(64),
  })
  .superRefine((v, ctx) => {
    if (v.cashCollected !== "" && v.cashMode === "") {
      ctx.addIssue({
        code: "custom",
        path: ["cashMode"],
        message: "Choose how the cash was collected.",
      });
    }
    if (v.cashCollected !== "" && v.cashMode === "UPI" && v.upiReference === "") {
      ctx.addIssue({ code: "custom", path: ["upiReference"], message: "Enter the UPI reference." });
    }
  });

function DeliverDialog({ shipment, onClose }: DialogProps) {
  const deliver = useDeliverShipment();
  const retryAfterConflict = useRetryAfterConflict(shipment, "deliver", onClose);
  const prepaid = isPrepaid(shipment);
  const form = useForm<z.infer<typeof deliverSchema>>({
    resolver: zodResolver(deliverSchema),
    defaultValues: { deliveryCode: "", cashCollected: "", cashMode: "", upiReference: "" },
  });
  const mode = useWatch({ control: form.control, name: "cashMode" });
  async function send(values: z.infer<typeof deliverSchema>, version: string): Promise<void> {
    const cash =
      prepaid || values.cashCollected === "" ? null : parseRupeesToPaise(values.cashCollected);
    try {
      await deliver.mutateAsync({
        id: shipment.id,
        version,
        body: {
          deliveryCode: values.deliveryCode,
          cashCollectedPaise: cash,
          cashMode: cash === null ? null : (values.cashMode as "CASH" | "UPI"),
          upiReference: cash !== null && values.cashMode === "UPI" ? values.upiReference : null,
        },
      });
      notifySaved("Delivered", "The invoice is issued from the final weights.");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["deliveryCode", "cashCollected", "cashMode", "upiReference"],
        aliases: { cashCollectedPaise: "cashCollected" },
        codeFields: {
          DELIVERY_CODE_INVALID: "deliveryCode",
          DELIVERY_CODE_LOCKED: "deliveryCode",
          CASH_ALREADY_COLLECTED: "cashCollected",
        },
        title: "Could not mark as delivered",
        onConflict: retryAfterConflict((next) => send(values, next)),
      });
    }
  }
  const onSubmit = form.handleSubmit((values) => send(values, shipment.version));
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="Deliver"
        description="Ask the customer for the 4-digit delivery code from their order tracking."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="deliver-shipment" loading={deliver.isPending}>
              Mark delivered
            </Button>
          </>
        }
      >
        <form id="deliver-shipment" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <ShipmentSummary shipment={shipment} />
          {shipment.deliveryCodeLocked && (
            <p className="text-warning-text text-sm">
              Too many wrong codes. Record a failed attempt and reattempt to unlock.
            </p>
          )}
          <TextField
            form={form}
            name="deliveryCode"
            label="Delivery code"
            inputMode="numeric"
            autoComplete="off"
            maxLength={4}
            required
          />
          {!prepaid && (
            <>
              <TextField
                form={form}
                name="cashCollected"
                label="Cash collected (₹)"
                hint={
                  shipment.amountToCollectPaise != null
                    ? `Collect ${formatRupees(shipment.amountToCollectPaise)}, or leave empty and record the collection later.`
                    : "Leave empty for prepaid orders, or record the collection later."
                }
                inputMode="decimal"
              />
              <SelectField
                form={form}
                name="cashMode"
                label="Collected as"
                options={CASH_MODES}
                placeholder="Cash or UPI"
              />
              {mode === "UPI" && (
                <TextField form={form} name="upiReference" label="UPI reference" />
              )}
            </>
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}

const attemptSchema = z
  .object({
    outcome: z.string().min(1, "Choose what happened."),
    notes: z.string().trim().max(500),
  })
  .superRefine((v, ctx) => {
    if (v.outcome === "OTHER" && v.notes === "") {
      ctx.addIssue({ code: "custom", path: ["notes"], message: "Describe what happened." });
    }
  });

function AttemptDialog({ shipment, onClose }: DialogProps) {
  const record = useRecordAttempt();
  const retryAfterConflict = useRetryAfterConflict(shipment, "attempt", onClose);
  const form = useForm<z.infer<typeof attemptSchema>>({
    resolver: zodResolver(attemptSchema),
    defaultValues: { outcome: "", notes: "" },
  });
  async function send(values: z.infer<typeof attemptSchema>, version: string): Promise<void> {
    try {
      await record.mutateAsync({
        id: shipment.id,
        version,
        body: {
          outcome: values.outcome as
            "CUSTOMER_UNAVAILABLE" | "ADDRESS_NOT_FOUND" | "REFUSED" | "OTHER",
          notes: values.notes || null,
        },
      });
      notifySaved("Failed attempt recorded");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["outcome", "notes"],
        onConflict: retryAfterConflict((next) => send(values, next)),
      });
    }
  }
  const onSubmit = form.handleSubmit((values) => send(values, shipment.version));
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="Record a failed attempt"
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="attempt-shipment"
              variant="danger"
              loading={record.isPending}
            >
              Record attempt
            </Button>
          </>
        }
      >
        <form id="attempt-shipment" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <ShipmentSummary shipment={shipment} />
          <SelectField form={form} name="outcome" label="Outcome" required options={OUTCOMES} />
          <TextAreaField
            form={form}
            name="notes"
            label="Notes"
            hint="Required for Other. Up to 500 characters."
            rows={3}
          />
        </form>
      </DialogContent>
    </Dialog>
  );
}

const cashSchema = z
  .object({
    amount: z
      .string()
      .trim()
      .min(1, "Enter the amount collected.")
      .refine(
        (v) => parseRupeesToPaise(v) !== null,
        "Enter an amount in rupees, e.g. 450 or 450.50.",
      ),
    mode: z.string().min(1, "Choose Cash or UPI."),
    upiReference: z.string().trim().max(64),
  })
  .superRefine((v, ctx) => {
    if (v.mode === "UPI" && v.upiReference === "") {
      ctx.addIssue({ code: "custom", path: ["upiReference"], message: "Enter the UPI reference." });
    }
  });

function CashDialog({ shipment, onClose }: DialogProps) {
  const record = useRecordCashCollected();
  const key = useIdempotencyKey();
  const due = shipment.amountToCollectPaise;
  const form = useForm<z.infer<typeof cashSchema>>({
    resolver: zodResolver(cashSchema),
    // AD-2: the server's amount to collect is the starting value; staff change it only if the rider got another.
    defaultValues: {
      amount: due != null ? paiseToRupeesInput(due) : "",
      mode: "",
      upiReference: "",
    },
  });
  const mode = useWatch({ control: form.control, name: "mode" });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await record.mutateAsync({
        id: shipment.id,
        idempotencyKey: key.current(),
        body: {
          amountPaise: parseRupeesToPaise(values.amount) ?? 0,
          mode: values.mode as "CASH" | "UPI",
          upiReference: values.mode === "UPI" ? values.upiReference : null,
        },
      });
      key.reset();
      notifySaved("Collection recorded");
      onClose();
    } catch (error) {
      // A refusal (4xx) is final for this body, so the corrected attempt gets a new key; a network failure or
      // 5xx keeps the key so the retry is recognised as the same action.
      if (isApiError(error) && error.status >= 400 && error.status < 500) key.reset();
      reportWriteError(error, {
        setError: form.setError,
        fields: ["amount", "mode", "upiReference"],
        aliases: { amountPaise: "amount" },
        codeFields: { CASH_ALREADY_COLLECTED: "amount", CASH_AMOUNT_MISMATCH: "amount" },
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="Record cash collected"
        description={
          due != null
            ? `Amount due: ${formatRupees(due)}. The amount must equal it.`
            : "The amount must equal the amount due on the order."
        }
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="cash-collected" loading={record.isPending}>
              Record
            </Button>
          </>
        }
      >
        <form id="cash-collected" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <ShipmentSummary shipment={shipment} />
          <TextField form={form} name="amount" label="Amount (₹)" inputMode="decimal" required />
          <SelectField form={form} name="mode" label="Collected as" required options={CASH_MODES} />
          {mode === "UPI" && (
            <TextField form={form} name="upiReference" label="UPI reference" required />
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ConfirmTransition({
  shipment,
  kind,
  onClose,
}: DialogProps & { kind: "reattempt" | "return" }) {
  const reattempt = useReattempt();
  const ret = useReturnShipment();
  const retryAfterConflict = useRetryAfterConflict(shipment, kind, onClose);
  const pending = reattempt.isPending || ret.isPending;
  async function run(version: string): Promise<void> {
    try {
      if (kind === "reattempt") await reattempt.mutateAsync({ id: shipment.id, version });
      else await ret.mutateAsync({ id: shipment.id, version });
      notifySaved(kind === "reattempt" ? "Out for delivery again" : "Shipment returned");
      onClose();
    } catch (error) {
      reportError(error, {
        title: kind === "reattempt" ? "Could not reattempt" : "Could not return",
        onConflict: retryAfterConflict((next) => run(next)),
      });
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title={kind === "reattempt" ? "Reattempt delivery?" : "Return shipment?"}
        description={
          kind === "reattempt"
            ? "The shipment goes out for delivery again with the same rider."
            : "The order is marked returned. Inventory staff record the stock that came back."
        }
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              variant={kind === "return" ? "danger" : "primary"}
              loading={pending}
              onClick={() => void run(shipment.version)}
            >
              {kind === "reattempt" ? "Reattempt" : "Return"}
            </Button>
          </>
        }
      />
    </Dialog>
  );
}
