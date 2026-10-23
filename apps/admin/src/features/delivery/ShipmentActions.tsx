import { isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent } from "@fresh/ui";
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
  useReturnShipment,
  useRiders,
} from "../../api/delivery";
import { type Shipment } from "../../api/types/delivery";
import { SelectField, TextAreaField, TextField } from "../../components/form";
import { notifySaved, reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import { parseRupeesToPaise } from "../../lib/units";
import { reportWriteError } from "./shared";

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

const optionalRupees = z
  .string()
  .trim()
  .refine(
    (v) => v === "" || parseRupeesToPaise(v) !== null,
    "Enter an amount in rupees, e.g. 450 or 450.50.",
  );

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
    shipment.cashCollectedPaise == null && (s === "OUT_FOR_DELIVERY" || s === "DELIVERED");

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

const assignSchema = z.object({ riderId: z.string().min(1, "Choose a rider.") });

function AssignDialog({ shipment, onClose }: DialogProps) {
  const riders = useRiders({
    fulfillmentLocationId: shipment.fulfillmentLocationId,
    isActive: true,
    pageSize: 100,
  });
  const assign = useAssignRider();
  const form = useForm<z.infer<typeof assignSchema>>({
    resolver: zodResolver(assignSchema),
    defaultValues: { riderId: shipment.riderId ?? "" },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await assign.mutateAsync({ id: shipment.id, version: shipment.version, body: values });
      notifySaved("Rider assigned");
      onClose();
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["riderId"],
        codeFields: { RIDER_NOT_AVAILABLE: "riderId" },
      });
    }
  });
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
  const form = useForm<z.infer<typeof dispatchSchema>>({
    resolver: zodResolver(dispatchSchema),
    defaultValues: { dispatchTemperatureC: "" },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      const result = await dispatch.mutateAsync({
        id: shipment.id,
        version: shipment.version,
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
      });
    }
  });
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
        <form id="dispatch-shipment" noValidate onSubmit={onSubmit}>
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
  const form = useForm<z.infer<typeof deliverSchema>>({
    resolver: zodResolver(deliverSchema),
    defaultValues: { deliveryCode: "", cashCollected: "", cashMode: "", upiReference: "" },
  });
  const mode = useWatch({ control: form.control, name: "cashMode" });
  const onSubmit = form.handleSubmit(async (values) => {
    const cash = values.cashCollected === "" ? null : parseRupeesToPaise(values.cashCollected);
    try {
      await deliver.mutateAsync({
        id: shipment.id,
        version: shipment.version,
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
      });
    }
  });
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
          <TextField
            form={form}
            name="cashCollected"
            label="Cash collected (₹)"
            hint={"Leave empty for prepaid orders, or record the collection later."}
            inputMode="decimal"
          />
          <SelectField
            form={form}
            name="cashMode"
            label="Collected as"
            options={CASH_MODES}
            placeholder="Cash or UPI"
          />
          {mode === "UPI" && <TextField form={form} name="upiReference" label="UPI reference" />}
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
  const form = useForm<z.infer<typeof attemptSchema>>({
    resolver: zodResolver(attemptSchema),
    defaultValues: { outcome: "", notes: "" },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await record.mutateAsync({
        id: shipment.id,
        version: shipment.version,
        body: {
          outcome: values.outcome as
            "CUSTOMER_UNAVAILABLE" | "ADDRESS_NOT_FOUND" | "REFUSED" | "OTHER",
          notes: values.notes || null,
        },
      });
      notifySaved("Failed attempt recorded");
      onClose();
    } catch (error) {
      reportWriteError(error, { setError: form.setError, fields: ["outcome", "notes"] });
    }
  });
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
  const form = useForm<z.infer<typeof cashSchema>>({
    resolver: zodResolver(cashSchema),
    defaultValues: { amount: "", mode: "", upiReference: "" },
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
        description="The amount must equal the amount due on the order."
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
  const pending = reattempt.isPending || ret.isPending;
  const run = async () => {
    try {
      if (kind === "reattempt")
        await reattempt.mutateAsync({ id: shipment.id, version: shipment.version });
      else await ret.mutateAsync({ id: shipment.id, version: shipment.version });
      notifySaved(kind === "reattempt" ? "Out for delivery again" : "Shipment returned");
      onClose();
    } catch (error) {
      reportError(error, {
        title: kind === "reattempt" ? "Could not reattempt" : "Could not return",
      });
    }
  };
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
              onClick={() => void run()}
            >
              {kind === "reattempt" ? "Reattempt" : "Return"}
            </Button>
          </>
        }
      />
    </Dialog>
  );
}
