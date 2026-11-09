"use client";

import { newIdempotencyKey } from "@fresh/core";
import {
  Button,
  Dialog,
  DialogContent,
  FormField,
  Input,
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
import { browserApiBaseUrl, browserHttp } from "@/api/browser";
import {
  addComplaintPhoto,
  cancelOrder,
  downloadInvoicePdf,
  raiseComplaint,
  reorder,
} from "@/api/endpoints/orders";
import { type CancelReasonCode, type ComplaintKind, type OrderDetail } from "@/api/types/orders";
import { asApiError, describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { saveFile } from "@/lib/save-file";

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
      const apiError = asApiError(e);
      // An unknown outcome (no answer, 5xx, retryable) keeps the key, so trying again cannot cancel twice; a definite
      // answer ends the action (D315).
      if (!apiError?.isOutcomeUnknown) actionKey.done();
      // Refused because the order moved on (409 INVALID_STATUS_TRANSITION, 422): say so, close, and read the order
      // again so the page shows the server's state and hides Cancel when it is no longer allowed.
      if (apiError && (apiError.status === 409 || apiError.status === 422)) {
        toast({ title: describeError(e), tone: "danger" });
        void queryClient.invalidateQueries({ queryKey: queryKeys.order(order.id) });
        void queryClient.invalidateQueries({ queryKey: queryKeys.tracking(order.id) });
        onClose();
        return;
      }
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
        onValueChange={(v) => {
          // Another reason is another request body: it needs its own Idempotency-Key.
          actionKey.done();
          setReason(v as CancelReasonCode);
        }}
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
  const [photos, setPhotos] = useState<File[]>([]);
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
      const complaint = await raiseComplaint(
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
      // The complaint exists now; photos go one by one, and a refused photo never undoes it.
      let photoError: string | null = null;
      for (const photo of photos) {
        try {
          await addComplaintPhoto(browserHttp(), order.id, complaint.id, photo);
        } catch (e) {
          photoError = describeError(e);
          break;
        }
      }
      if (photoError) toast({ title: photoError, tone: "danger" });
      void queryClient.invalidateQueries({ queryKey: queryKeys.complaints(order.id) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.order(order.id) });
      toast({ title: "We have your complaint and will get back to you.", tone: "success" });
      onClose();
    } catch (e) {
      // Only an unknown outcome (no answer, 5xx, 429, IDEMPOTENCY_IN_PROGRESS) may resend the same key (D315); after a
      // refusal the customer changes the form, which is a new body and needs a new key (D224).
      if (!asApiError(e)?.isOutcomeUnknown) actionKey.done();
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
        <FormField label="Photos (optional)">
          <Input
            type="file"
            accept="image/*"
            multiple
            onChange={(e) => setPhotos(Array.from(e.target.files ?? []))}
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

/** INVOICE_PDF_NOT_READY: the invoice is issued, its PDF is still being rendered (D343). */
export const INVOICE_PREPARING = "Invoice is being prepared, try again in a minute.";
/** INVOICE_NOT_ISSUED: the invoice is made when the order is delivered. */
export const INVOICE_AFTER_DELIVERY = "The invoice is made when your order is delivered.";

interface InvoiceNote {
  tone: "info" | "danger";
  text: string;
}

/** Only the actions the server allows for this order (`allowedActions`). */
export function OrderActions({ order }: { order: OrderDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<"cancel" | "complaint" | null>(null);
  const [busy, setBusy] = useState<"reorder" | "invoice" | null>(null);
  const [invoiceNote, setInvoiceNote] = useState<InvoiceNote | null>(null);
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

  /**
   * "Download invoice" (ORD-R53): the PDF is fetched with the session's bearer token (its `pdfUrl` is an
   * authenticated API path, D343, so a plain link or new tab would arrive signed out) and saved under the server's
   * file name. A PDF still being rendered is a calm note, not an error; the button stays for another try.
   */
  async function downloadInvoice() {
    setBusy("invoice");
    setInvoiceNote(null);
    try {
      const file = await downloadInvoicePdf(browserHttp(), browserApiBaseUrl(), order.id);
      saveFile(file.blob, file.fileName);
    } catch (e) {
      if (hasCode(e, "INVOICE_PDF_NOT_READY")) {
        setInvoiceNote({ tone: "info", text: INVOICE_PREPARING });
      } else if (hasCode(e, "INVOICE_NOT_ISSUED")) {
        setInvoiceNote({ tone: "info", text: INVOICE_AFTER_DELIVERY });
      } else {
        setInvoiceNote({ tone: "danger", text: describeError(e) });
      }
    } finally {
      setBusy(null);
    }
  }

  if (!actions.cancel && !actions.raiseComplaint && !actions.reorder && !actions.downloadInvoice) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-2">
        {actions.reorder && (
          <Button loading={busy === "reorder"} onClick={() => void doReorder()}>
            Order again
          </Button>
        )}
        {actions.downloadInvoice && (
          <Button
            variant="outline"
            loading={busy === "invoice"}
            onClick={() => void downloadInvoice()}
          >
            Download invoice
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
      {invoiceNote && (
        <p
          role={invoiceNote.tone === "danger" ? "alert" : "status"}
          className={invoiceNote.tone === "danger" ? "text-danger-text text-sm" : "text-sm"}
        >
          {invoiceNote.text}
        </p>
      )}
    </div>
  );
}
