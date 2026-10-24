"use client";

import { newIdempotencyKey } from "@fresh/core";
import {
  Button,
  Dialog,
  DialogContent,
  FormField,
  RadioGroup,
  Select,
  Sheet,
  SheetContent,
  Textarea,
  toast,
} from "@fresh/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { type FormEvent, useRef, useState } from "react";
import { browserHttp } from "@/api/browser";
import { cancelOrder, getInvoice, raiseComplaint, reorder } from "@/api/endpoints/orders";
import { type CancelReasonCode, type ComplaintKind, type OrderDetail } from "@/api/types/orders";
import { describeError, fieldMessage } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const CANCEL_REASONS: { value: CancelReasonCode; label: string }[] = [
  { value: "CHANGED_MIND", label: "I changed my mind" },
  { value: "WRONG_ADDRESS", label: "Wrong address" },
  { value: "SLOT_ISSUE", label: "The delivery slot does not suit me" },
  { value: "OTHER", label: "Something else" },
];

const COMPLAINT_KINDS: { value: ComplaintKind; label: string }[] = [
  { value: "MISSING", label: "Item missing" },
  { value: "DAMAGED", label: "Item damaged" },
  { value: "QUALITY", label: "Quality problem" },
  { value: "WRONG_ITEM", label: "Wrong item" },
  { value: "LATE", label: "Delivered late" },
];

const WHOLE_ORDER = "__order__";

/** Keeps one Idempotency-Key per user action, reused while that action is retried. */
function useActionKey() {
  const key = useRef<string | null>(null);
  return {
    get: () => (key.current ??= newIdempotencyKey()),
    done: () => {
      key.current = null;
    },
  };
}

function CancelDialog({ order, onClose }: { order: OrderDetail; onClose: () => void }) {
  const queryClient = useQueryClient();
  const actionKey = useActionKey();
  const [reason, setReason] = useState<CancelReasonCode>("CHANGED_MIND");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      const updated = await cancelOrder(browserHttp(), order.id, reason, actionKey.get());
      actionKey.done();
      queryClient.setQueryData(queryKeys.order(order.id), updated);
      void queryClient.invalidateQueries({ queryKey: queryKeys.tracking(order.id) });
      void queryClient.invalidateQueries({ queryKey: ["orders"] });
      toast({ title: `Order ${order.orderNumber} cancelled`, tone: "success" });
      onClose();
    } catch (e) {
      setError(describeError(e));
    } finally {
      setPending(false);
    }
  }

  return (
    <DialogContent
      title={`Cancel order ${order.orderNumber}?`}
      description="Tell us why so we can improve."
      size="sm"
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Keep order
          </Button>
          <Button variant="danger" loading={pending} onClick={() => void confirm()}>
            Cancel order
          </Button>
        </>
      }
    >
      <RadioGroup
        label="Reason"
        value={reason}
        onValueChange={(v) => setReason(v as CancelReasonCode)}
        options={CANCEL_REASONS}
      />
      {error && (
        <p role="alert" className="text-danger-text text-sm">
          {error}
        </p>
      )}
    </DialogContent>
  );
}

function ComplaintSheet({ order, onClose }: { order: OrderDetail; onClose: () => void }) {
  const queryClient = useQueryClient();
  const actionKey = useActionKey();
  const [itemId, setItemId] = useState<string>(WHOLE_ORDER);
  const [kind, setKind] = useState<ComplaintKind>("QUALITY");
  const [description, setDescription] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const local: Record<string, string> = {};
    if (description.trim().length < 10)
      local.description = "Describe the problem in a sentence or two.";
    setErrors(local);
    setFormError(null);
    if (Object.keys(local).length) return;
    setPending(true);
    try {
      await raiseComplaint(
        browserHttp(),
        order.id,
        {
          orderItemId: itemId === WHOLE_ORDER ? null : itemId,
          kind,
          description: description.trim(),
        },
        actionKey.get(),
      );
      actionKey.done();
      void queryClient.invalidateQueries({ queryKey: queryKeys.complaints(order.id) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.order(order.id) });
      toast({ title: "We have your complaint and will get back to you.", tone: "success" });
      onClose();
    } catch (e) {
      const fields: Record<string, string> = {};
      for (const field of ["orderItemId", "kind", "description"]) {
        const message = fieldMessage(e, field);
        if (message) fields[field] = message;
      }
      setErrors(fields);
      if (!Object.keys(fields).length) setFormError(describeError(e));
    } finally {
      setPending(false);
    }
  }

  return (
    <SheetContent title="Report a problem" description={`Order ${order.orderNumber}`}>
      <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-4">
        <FormField label="Which item?" error={errors.orderItemId}>
          <Select
            value={itemId}
            onValueChange={setItemId}
            options={[
              { value: WHOLE_ORDER, label: "The whole order" },
              ...order.items.map((item) => ({
                value: item.id,
                label: `${item.productName} (${item.variantName})`,
              })),
            ]}
          />
        </FormField>
        <RadioGroup
          label="What went wrong?"
          value={kind}
          onValueChange={(v) => setKind(v as ComplaintKind)}
          options={COMPLAINT_KINDS}
          error={errors.kind}
        />
        <FormField label="Details" error={errors.description} required>
          <Textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={4}
            maxLength={1000}
          />
        </FormField>
        {formError && (
          <p role="alert" className="text-danger-text text-sm">
            {formError}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button type="submit" loading={pending}>
            Send
          </Button>
        </div>
      </form>
    </SheetContent>
  );
}

/** Only the actions the server allows for this order (`allowedActions`). */
export function OrderActions({ order }: { order: OrderDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<"cancel" | "complaint" | null>(null);
  const [busy, setBusy] = useState<"reorder" | "invoice" | null>(null);
  const actions = order.allowedActions;

  async function doReorder() {
    setBusy("reorder");
    try {
      const cart = await reorder(browserHttp(), order.id);
      queryClient.setQueryData(queryKeys.cart, cart);
      router.push("/cart");
    } catch (e) {
      toast({ title: describeError(e), tone: "danger" });
    } finally {
      setBusy(null);
    }
  }

  async function openInvoice() {
    setBusy("invoice");
    try {
      const invoice = await getInvoice(browserHttp(), order.id);
      if (invoice.pdfUrl) window.open(invoice.pdfUrl, "_blank", "noopener");
      else toast({ title: "Your invoice is being prepared. Please try again shortly." });
    } catch (e) {
      toast({ title: describeError(e), tone: "danger" });
    } finally {
      setBusy(null);
    }
  }

  if (!actions.cancel && !actions.raiseComplaint && !actions.reorder && !actions.downloadInvoice) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {actions.reorder && (
        <Button loading={busy === "reorder"} onClick={() => void doReorder()}>
          Order again
        </Button>
      )}
      {actions.downloadInvoice && (
        <Button variant="outline" loading={busy === "invoice"} onClick={() => void openInvoice()}>
          Invoice
        </Button>
      )}
      {actions.raiseComplaint && (
        <Sheet open={open === "complaint"} onOpenChange={(o) => setOpen(o ? "complaint" : null)}>
          <Button variant="outline" onClick={() => setOpen("complaint")}>
            Report a problem
          </Button>
          {open === "complaint" && <ComplaintSheet order={order} onClose={() => setOpen(null)} />}
        </Sheet>
      )}
      {actions.cancel && (
        <Dialog open={open === "cancel"} onOpenChange={(o) => setOpen(o ? "cancel" : null)}>
          <Button variant="ghost" onClick={() => setOpen("cancel")}>
            Cancel order
          </Button>
          {open === "cancel" && <CancelDialog order={order} onClose={() => setOpen(null)} />}
        </Dialog>
      )}
    </div>
  );
}
