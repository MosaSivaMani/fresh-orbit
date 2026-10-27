import { formatRupees, isApiError } from "@fresh/core";
import { Button, Dialog, DialogContent, FormField, Input, Select, Textarea } from "@fresh/ui";
import { useId, useState } from "react";
import { useCreateCashSettlement } from "../../api/payments";
import { type CashSettlement, type RiderOption } from "../../api/types/payments";
import { reportError } from "../../lib/feedback";
import { useIdempotencyKey } from "../../lib/idempotency";
import { parseRupeesToPaise, todayInIndia } from "../../lib/units";
import { Banner, PAYMENT_CODE_MESSAGES } from "./shared";

type Field = "riderId" | "date" | "receivedPaise" | "note";

/** Where a refusal code belongs on this form (the server may also send field-keyed `errors`). */
const CODE_FIELDS: Readonly<Record<string, Field>> = {
  DATE_IN_FUTURE: "date",
  SETTLEMENT_EXISTS: "date",
  RIDER_NOT_FOUND: "riderId",
};

/**
 * Record what a rider handed in for one day. The server computes the expected cash and the difference; this
 * dialog shows them from the response. One Idempotency-Key per attempt, reused on retry.
 */
export function RecordSettlementDialog({
  open,
  onOpenChange,
  riders,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** null when the rider lookup is refused (403): the form asks for the rider id instead. */
  riders: readonly RiderOption[] | null;
}) {
  const create = useCreateCashSettlement();
  const key = useIdempotencyKey();
  const formId = useId();
  const [riderId, setRiderId] = useState("");
  const [date, setDate] = useState(todayInIndia);
  const [received, setReceived] = useState("");
  const [note, setNote] = useState("");
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [result, setResult] = useState<CashSettlement | null>(null);

  const close = (next: boolean) => {
    if (!next) {
      setRiderId("");
      setDate(todayInIndia());
      setReceived("");
      setNote("");
      setErrors({});
      setBanner(null);
      setResult(null);
    }
    onOpenChange(next);
  };

  // Changing what is submitted makes it a new action, so it gets a new key.
  const edit = (apply: () => void) => {
    apply();
    key.reset();
  };

  const submit = () => {
    const receivedPaise = parseRupeesToPaise(received);
    const local: Partial<Record<Field, string>> = {};
    if (!riderId.trim()) local.riderId = "Choose the rider.";
    if (!date) local.date = "Enter the date of the collections.";
    if (receivedPaise === null)
      local.receivedPaise = "Enter the received amount in rupees, e.g. 1250 or 1250.50.";
    if (note.length > 500) local.note = "Note must be at most 500 characters.";
    setErrors(local);
    setBanner(null);
    if (Object.keys(local).length > 0 || receivedPaise === null) return;

    create.mutate(
      {
        body: { riderId: riderId.trim(), date, receivedPaise, note: note.trim() || null },
        idempotencyKey: key.current(),
      },
      {
        onSuccess: (settlement) => {
          key.reset();
          setResult(settlement);
        },
        onError: (error) => {
          if (!isApiError(error))
            return reportError(error, { title: "Could not record the settlement" });
          const fieldErrors: Partial<Record<Field, string>> = {};
          for (const f of ["riderId", "date", "receivedPaise", "note"] as const) {
            const message = error.fieldMessage(f);
            if (message) fieldErrors[f] = message;
          }
          const codeField = CODE_FIELDS[error.code];
          if (codeField && !fieldErrors[codeField])
            fieldErrors[codeField] =
              PAYMENT_CODE_MESSAGES[error.code] ??
              (error.code === "DATE_IN_FUTURE"
                ? "The date cannot be in the future."
                : (error.problem.detail ?? error.code));
          if (Object.keys(fieldErrors).length > 0) setErrors(fieldErrors);
          else if (error.status === 409 || error.status === 422)
            setBanner(PAYMENT_CODE_MESSAGES[error.code] ?? error.message);
          else reportError(error, { title: "Could not record the settlement" });
        },
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent
        title={result ? "Settlement recorded" : "Record cash settlement"}
        description={
          result
            ? "Expected cash and the difference are computed by the server from the rider's collections."
            : "Enter the cash and UPI-on-delivery total the rider handed in for one day."
        }
        footer={
          result ? (
            <Button onClick={() => close(false)}>Done</Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => close(false)}>
                Cancel
              </Button>
              <Button type="submit" form={formId} loading={create.isPending}>
                Record settlement
              </Button>
            </>
          )
        }
      >
        {result ? (
          <dl className="grid grid-cols-3 gap-3" aria-label="Settlement result">
            <div>
              <dt className="text-text-muted text-sm">Expected</dt>
              <dd className="font-semibold tabular-nums">{formatRupees(result.expectedPaise)}</dd>
            </div>
            <div>
              <dt className="text-text-muted text-sm">Received</dt>
              <dd className="font-semibold tabular-nums">{formatRupees(result.receivedPaise)}</dd>
            </div>
            <div>
              <dt className="text-text-muted text-sm">Difference</dt>
              <dd
                className={`font-semibold tabular-nums ${result.differencePaise === 0 ? "" : "text-danger-text"}`}
                data-testid="settlement-difference"
              >
                {formatRupees(result.differencePaise)}
              </dd>
            </div>
            <p className="text-text-muted col-span-3 text-sm">
              {result.collectionsCount} collection{result.collectionsCount === 1 ? "" : "s"}{" "}
              counted.
            </p>
          </dl>
        ) : (
          <form
            id={formId}
            noValidate
            className="flex flex-col gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              submit();
            }}
          >
            {banner && <Banner>{banner}</Banner>}
            <FormField label="Rider" required error={errors.riderId}>
              {riders ? (
                <Select
                  options={riders.map((r) => ({
                    value: r.id,
                    label: `${r.fullName} · ••${r.phoneLast4}`,
                  }))}
                  value={riderId}
                  onValueChange={(v) => edit(() => setRiderId(v))}
                  placeholder="Choose a rider"
                />
              ) : (
                <Input
                  value={riderId}
                  onChange={(e) => edit(() => setRiderId(e.target.value))}
                  placeholder="Rider id"
                />
              )}
            </FormField>
            <FormField
              label="Date"
              required
              error={errors.date}
              hint="The local business day of the collections."
            >
              <Input
                type="date"
                value={date}
                max={todayInIndia()}
                onChange={(e) => edit(() => setDate(e.target.value))}
              />
            </FormField>
            <FormField label="Received (₹)" required error={errors.receivedPaise}>
              <Input
                inputMode="decimal"
                value={received}
                onChange={(e) => edit(() => setReceived(e.target.value))}
                placeholder="0.00"
              />
            </FormField>
            <FormField label="Note" error={errors.note}>
              <Textarea
                rows={2}
                maxLength={500}
                value={note}
                onChange={(e) => edit(() => setNote(e.target.value))}
              />
            </FormField>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
