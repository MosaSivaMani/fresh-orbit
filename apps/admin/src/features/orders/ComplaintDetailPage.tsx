import { zodResolver } from "@hookform/resolvers/zod";
import { formatDateTime, formatRupees, humanizeCode } from "@fresh/core";
import { Button, StatusBadge } from "@fresh/ui";
import { CheckCircle2 } from "lucide-react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useParams } from "react-router";
import { z } from "zod";
import { useComplaint, useDecideComplaint, useResolveComplaint } from "../../api/orders";
import { type ComplaintDetail } from "../../api/types/orders";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { SelectField, TextAreaField, TextField } from "../../components/form";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { COMPLAINT_STATUS } from "../../lib/status";
import { parseRupeesToPaise } from "../../lib/units";

type Complaint = ComplaintDetail & { version: string };

export function ComplaintDetailPage() {
  const { complaintId = "" } = useParams();
  const query = useComplaint(complaintId);
  return (
    <QueryView query={query} what="complaint">
      {(complaint) => <ComplaintView complaint={complaint} reload={() => query.refetch()} />}
    </QueryView>
  );
}

function ComplaintView({ complaint, reload }: { complaint: Complaint; reload: () => unknown }) {
  const can = useCan();
  const { nameOf } = useLocationScope();
  const resolve = useResolveComplaint(complaint.id);

  return (
    <>
      <PageHeader
        backTo="/complaints"
        backLabel="Complaints"
        title={`${humanizeCode(complaint.kind)} complaint`}
        meta={<StatusBadge code={complaint.status} map={COMPLAINT_STATUS} />}
        description={`Order ${complaint.orderNumber} · raised ${formatDateTime(complaint.raisedAt)}`}
        actions={
          complaint.status === "APPROVED" && (
            <Button
              leadingIcon={<CheckCircle2 aria-hidden="true" />}
              loading={resolve.isPending}
              onClick={() =>
                resolve.mutate(complaint.version, {
                  onSuccess: () => notifySaved("Complaint resolved"),
                  onError: (error) => reportError(error, { onConflict: reload }),
                })
              }
            >
              Mark resolved
            </Button>
          )
        }
      />
      <div className="flex flex-col gap-4">
        <Section title="Details">
          <DetailList
            items={[
              {
                label: "Order",
                value: can("orders.view") ? (
                  <Link
                    to={`/orders/${complaint.orderId}`}
                    className="text-accent-text hover:underline"
                  >
                    {complaint.orderNumber}
                  </Link>
                ) : (
                  complaint.orderNumber
                ),
              },
              { label: "Item", value: complaint.orderItemId ? "One item" : "Whole order" },
              { label: "Location", value: nameOf(complaint.fulfillmentLocationId) },
              {
                label: "Resolution",
                value: complaint.resolution ? humanizeCode(complaint.resolution) : "—",
              },
              {
                label: "Refund amount",
                value:
                  complaint.refundAmountPaise != null
                    ? formatRupees(complaint.refundAmountPaise)
                    : "—",
              },
              {
                label: "Decided",
                value: complaint.decidedAt ? formatDateTime(complaint.decidedAt) : "—",
              },
              { label: "Decision note", value: complaint.decisionNote ?? "—" },
              {
                label: "Resolved",
                value: complaint.resolvedAt ? formatDateTime(complaint.resolvedAt) : "—",
              },
            ]}
          />
          <p className="mt-3 text-sm whitespace-pre-wrap">{complaint.description}</p>
        </Section>
        {complaint.attachmentUrls.length > 0 && (
          <Section title="Photos">
            <ul className="flex flex-wrap gap-3">
              {complaint.attachmentUrls.map((url) => (
                <li key={url}>
                  <a href={url} target="_blank" rel="noreferrer">
                    <img
                      src={url}
                      alt="Complaint attachment"
                      className="border-border size-32 rounded-md border object-cover"
                    />
                  </a>
                </li>
              ))}
            </ul>
          </Section>
        )}
        {complaint.status === "OPEN" && <DecideForm complaint={complaint} reload={reload} />}
      </div>
    </>
  );
}

const decideSchema = z
  .object({
    status: z.enum(["APPROVED", "REJECTED"]),
    resolution: z.enum(["REFUND", "REPLACEMENT", "NONE"]),
    refundAmount: z.string().trim(),
    notes: z.string().trim().max(500, "Keep notes under 500 characters."),
  })
  .superRefine((v, ctx) => {
    if (v.status === "APPROVED" && v.resolution === "REFUND") {
      const paise = parseRupeesToPaise(v.refundAmount);
      if (paise === null || paise <= 0)
        ctx.addIssue({
          code: "custom",
          path: ["refundAmount"],
          message: "Enter the refund amount in rupees.",
        });
    }
    if (v.status === "REJECTED" && !v.notes)
      ctx.addIssue({
        code: "custom",
        path: ["notes"],
        message: "Tell the customer why the complaint is rejected.",
      });
  });

type DecideValues = z.infer<typeof decideSchema>;

function DecideForm({ complaint, reload }: { complaint: Complaint; reload: () => unknown }) {
  const decide = useDecideComplaint(complaint.id);
  const form = useForm<DecideValues>({
    resolver: zodResolver(decideSchema),
    defaultValues: { status: "APPROVED", resolution: "REFUND", refundAmount: "", notes: "" },
  });
  const status = useWatch({ control: form.control, name: "status" });
  const resolution = useWatch({ control: form.control, name: "resolution" });

  const onSubmit = form.handleSubmit((v) => {
    const approved = v.status === "APPROVED";
    decide.mutate(
      {
        version: complaint.version,
        body: {
          status: v.status,
          resolution: approved ? v.resolution : "NONE",
          refundAmountPaise:
            approved && v.resolution === "REFUND" ? parseRupeesToPaise(v.refundAmount) : null,
          note: v.notes || null,
        },
      },
      {
        onSuccess: () => notifySaved(approved ? "Complaint approved" : "Complaint rejected"),
        onError: (error) =>
          reportError(error, {
            setError: form.setError,
            fields: ["status", "resolution", "refundAmount", "notes"],
            aliases: { refundAmountPaise: "refundAmount" },
            onConflict: reload,
            title: "Could not decide the complaint",
          }),
      },
    );
  });

  return (
    <Section title="Decide">
      <form
        noValidate
        onSubmit={onSubmit}
        className="flex max-w-xl flex-col gap-4"
        aria-label="Decide complaint"
      >
        <SelectField
          form={form}
          name="status"
          label="Decision"
          options={[
            { value: "APPROVED", label: "Approve" },
            { value: "REJECTED", label: "Reject" },
          ]}
        />
        {status === "APPROVED" && (
          <SelectField
            form={form}
            name="resolution"
            label="Resolution"
            options={[
              { value: "REFUND", label: "Refund" },
              { value: "REPLACEMENT", label: "Replacement" },
              { value: "NONE", label: "No action" },
            ]}
          />
        )}
        {status === "APPROVED" && resolution === "REFUND" && (
          <TextField
            form={form}
            name="refundAmount"
            label="Refund amount (₹)"
            inputMode="decimal"
            hint="The server checks the amount against the line total."
          />
        )}
        <TextAreaField form={form} name="notes" label="Notes" required={status === "REJECTED"} />
        <div>
          <Button type="submit" loading={decide.isPending}>
            Save decision
          </Button>
        </div>
      </form>
    </Section>
  );
}
