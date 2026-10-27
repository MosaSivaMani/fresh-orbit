import { formatQuantity, humanizeCode } from "@fresh/core";
import { Button, Dialog, DialogContent, FormField, Input, Select, Textarea } from "@fresh/ui";
import { useState } from "react";
import {
  useCancelOrder,
  useGoodwill,
  useProposeSubstitution,
  useRecordSubstitutionResponse,
} from "../../api/orders";
import { usePreparationMethods } from "../../api/catalog";
import {
  type AdminOrderDetail,
  type OrderItem,
  STAFF_CANCEL_REASONS,
  type StaffCancelReason,
  type Substitution,
} from "../../api/types/orders";
import {
  type PickedVariant,
  type VariantChoice,
  VariantPicker,
} from "../../components/VariantPicker";
import { notifySaved, reportError } from "../../lib/feedback";
import { parseRupeesToPaise, parseWholeNumber } from "../../lib/units";

type Order = AdminOrderDetail & { version: string };

function firstFieldError(error: unknown, ...fields: string[]): string | undefined {
  const errors = (error as { errors?: Record<string, { message: string }[]> } | null)?.errors;
  if (!errors) return undefined;
  for (const f of fields) {
    const m = errors[f]?.[0]?.message;
    if (m) return m;
  }
  return undefined;
}

export function CancelOrderDialog({
  order,
  open,
  onOpenChange,
  onConflict,
}: {
  order: Order;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const cancel = useCancelOrder(order.id);
  const [reason, setReason] = useState<StaffCancelReason | "">("");
  const [notes, setNotes] = useState("");
  const [touched, setTouched] = useState(false);
  const reasonError =
    touched && !reason
      ? "Choose why the order is cancelled."
      : firstFieldError(cancel.error, "reasonCode");
  const notesError =
    touched && reason === "OTHER" && !notes.trim()
      ? "Describe the reason in notes."
      : firstFieldError(cancel.error, "note");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title={`Cancel ${order.orderNumber}?`}
        description={
          order.status === "PACKED"
            ? "The order is packed: its stock is already used. Record a return or waste in Inventory afterwards."
            : "Reserved stock, the delivery slot and any coupon are released by the server."
        }
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Keep order
            </Button>
            <Button
              variant="danger"
              loading={cancel.isPending}
              onClick={() => {
                setTouched(true);
                if (!reason || (reason === "OTHER" && !notes.trim())) return;
                cancel.mutate(
                  {
                    body: { reasonCode: reason, note: notes.trim() || null },
                    version: order.version,
                  },
                  {
                    onSuccess: () => {
                      notifySaved(`${order.orderNumber} cancelled`);
                      onOpenChange(false);
                    },
                    onError: (error) => {
                      if (!firstFieldError(error, "reasonCode", "note"))
                        reportError(error, { onConflict });
                    },
                  },
                );
              }}
            >
              Cancel order
            </Button>
          </>
        }
      >
        <FormField label="Reason" required error={reasonError}>
          <Select
            options={STAFF_CANCEL_REASONS.map((r) => ({ value: r, label: humanizeCode(r) }))}
            value={reason}
            placeholder="Choose a reason"
            onValueChange={(v) => setReason(v as StaffCancelReason)}
          />
        </FormField>
        <FormField label="Notes" required={reason === "OTHER"} error={notesError}>
          <Textarea value={notes} maxLength={500} onChange={(e) => setNotes(e.target.value)} />
        </FormField>
      </DialogContent>
    </Dialog>
  );
}

export function ProposeSubstitutionDialog({
  order,
  item,
  open,
  onOpenChange,
  onConflict,
}: {
  order: Order;
  item: OrderItem;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const propose = useProposeSubstitution(order.id);
  const [choice, setChoice] = useState<VariantChoice>({
    productId: null,
    variantId: "",
    preparationCode: "",
  });
  const [variant, setVariant] = useState<PickedVariant | null>(null);
  const [quantity, setQuantity] = useState("");
  const [touched, setTouched] = useState(false);
  const unit = variant
    ? variant.stockUnit === "GRAM"
      ? "GRAM"
      : "PIECE"
    : item.orderedQuantity.unit;
  const parsed = parseWholeNumber(quantity);
  // The API takes the preparation method id; the picker gives its code. Any staff member reads the methods (G3).
  const methods = usePreparationMethods(open);
  const preparationMethodId =
    methods.data?.find((m) => m.code === choice.preparationCode)?.id ?? null;

  const errors = {
    variantId:
      (touched && !choice.variantId ? "Choose the substitute variant." : undefined) ??
      firstFieldError(propose.error, "variantId"),
    preparationCode:
      (touched && !choice.preparationCode ? "Choose a preparation." : undefined) ??
      (touched && choice.preparationCode && !preparationMethodId
        ? "Preparation methods could not be loaded; ask a catalogue manager."
        : undefined) ??
      firstFieldError(propose.error, "preparationMethodId"),
  };
  const quantityError =
    (touched && (parsed === null || parsed <= 0)
      ? "Enter the quantity as a whole number."
      : undefined) ?? firstFieldError(propose.error, "quantity.value", "quantity");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        title="Propose a substitution"
        description={`For line ${item.lineNo}: ${item.productName} – ${item.variantName}, ${formatQuantity(item.orderedQuantity)}. ${
          order.substitutionPreference === "AUTO_SIMILAR"
            ? "The customer accepts similar items automatically when the substitute costs no more."
            : "The customer is asked to accept; you can also record their answer by phone."
        }`}
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Close
            </Button>
            <Button
              loading={propose.isPending}
              onClick={() => {
                setTouched(true);
                if (!choice.variantId || !preparationMethodId || parsed === null || parsed <= 0)
                  return;
                propose.mutate(
                  {
                    itemId: item.id,
                    version: order.version,
                    body: {
                      variantId: choice.variantId,
                      preparationMethodId,
                      quantity: { value: parsed, unit },
                    },
                  },
                  {
                    onSuccess: () => {
                      notifySaved("Substitution proposed");
                      onOpenChange(false);
                    },
                    onError: (error) => {
                      if (
                        !firstFieldError(
                          error,
                          "variantId",
                          "preparationMethodId",
                          "quantity.value",
                          "quantity",
                        )
                      )
                        reportError(error, { onConflict });
                    },
                  },
                );
              }}
            >
              Propose
            </Button>
          </>
        }
      >
        <VariantPicker value={choice} onChange={setChoice} errors={errors} onVariant={setVariant} />
        <FormField
          label={unit === "GRAM" ? "Quantity (grams)" : "Quantity (pieces)"}
          required
          error={quantityError}
          hint={
            variant?.orderStep !== undefined
              ? `Order step ${variant.orderStep}, min ${variant.minOrder}, max ${variant.maxOrder}.`
              : undefined
          }
        >
          <Input
            inputMode="numeric"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
          />
        </FormField>
      </DialogContent>
    </Dialog>
  );
}

export function RecordSubstitutionAnswer({
  order,
  substitution,
  onConflict,
}: {
  order: Order;
  substitution: Substitution;
  onConflict: () => unknown;
}) {
  const record = useRecordSubstitutionResponse(order.id);
  const send = (accepted: boolean) =>
    record.mutate(
      {
        substitutionId: substitution.id,
        body: { answer: accepted ? "ACCEPTED" : "REJECTED" },
        version: order.version,
      },
      {
        onSuccess: () => notifySaved(accepted ? "Substitution accepted" : "Substitution rejected"),
        onError: (error) => reportError(error, { onConflict }),
      },
    );
  return (
    <span className="flex gap-2">
      <Button
        size="sm"
        variant="outline"
        loading={record.isPending && record.variables?.body.answer === "ACCEPTED"}
        onClick={() => send(true)}
      >
        Customer accepted
      </Button>
      <Button
        size="sm"
        variant="ghost"
        loading={record.isPending && record.variables?.body.answer === "REJECTED"}
        onClick={() => send(false)}
      >
        Customer rejected
      </Button>
    </span>
  );
}

export function GoodwillDialog({
  order,
  open,
  onOpenChange,
  onConflict,
}: {
  order: Order;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConflict: () => unknown;
}) {
  const goodwill = useGoodwill(order.id);
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [itemId, setItemId] = useState("__order");
  const [touched, setTouched] = useState(false);
  const paise = parseRupeesToPaise(amount);
  const amountError =
    (touched && (paise === null || paise <= 0)
      ? "Enter the goodwill amount in rupees."
      : undefined) ?? firstFieldError(goodwill.error, "amountPaise");
  const reasonError =
    (touched && !reason.trim() ? "Enter the reason for the goodwill credit." : undefined) ??
    firstFieldError(goodwill.error, "reason");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title="Goodwill credit"
        description="Reduces the amount due before cash collection; after payment it becomes a refund request."
        footer={
          <>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Close
            </Button>
            <Button
              loading={goodwill.isPending}
              onClick={() => {
                setTouched(true);
                if (paise === null || paise <= 0 || !reason.trim()) return;
                goodwill.mutate(
                  {
                    version: order.version,
                    // The API takes the adjustment as a negative amount (ORD-R58).
                    body: {
                      amountPaise: -paise,
                      reason: reason.trim(),
                      orderItemId: itemId === "__order" ? null : itemId,
                    },
                  },
                  {
                    onSuccess: () => {
                      notifySaved("Goodwill credit recorded");
                      onOpenChange(false);
                    },
                    onError: (error) => {
                      if (!firstFieldError(error, "amountPaise", "reason"))
                        reportError(error, { onConflict });
                    },
                  },
                );
              }}
            >
              Record credit
            </Button>
          </>
        }
      >
        <FormField label="Amount (₹)" required error={amountError}>
          <Input
            inputMode="decimal"
            startAdornment="₹"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </FormField>
        <FormField label="Line">
          <Select
            options={[
              { value: "__order", label: "Whole order" },
              ...order.items.map((i) => ({
                value: i.id,
                label: `${i.lineNo}. ${i.productName} – ${i.variantName}`,
              })),
            ]}
            value={itemId}
            onValueChange={setItemId}
          />
        </FormField>
        <FormField label="Reason" required error={reasonError}>
          <Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </FormField>
      </DialogContent>
    </Dialog>
  );
}
