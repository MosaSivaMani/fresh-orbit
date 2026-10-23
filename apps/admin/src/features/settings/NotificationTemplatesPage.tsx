import { zodResolver } from "@hookform/resolvers/zod";
import { humanizeCode, isApiError } from "@fresh/core";
import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  StatusBadge,
  Switch,
} from "@fresh/ui";
import { Mail } from "lucide-react";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import {
  useNotificationTemplates,
  useSendTestTemplate,
  useUpdateTemplate,
} from "../../api/notifications";
import { type NotificationTemplate } from "../../api/types/notifications";
import { TextAreaField, TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { ACTIVE_FLAG } from "../../lib/status";

export function NotificationTemplatesPage() {
  const templates = useNotificationTemplates();
  const [editing, setEditing] = useState<NotificationTemplate | null>(null);
  const rows = templates.data ?? [];

  const columns = useMemo<ColumnDef<NotificationTemplate>[]>(
    () => [
      {
        id: "code",
        header: "Template",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span className="font-medium">{humanizeCode(row.original.code)}</span>
            {row.original.category && (
              <span className="text-text-muted text-xs">{humanizeCode(row.original.category)}</span>
            )}
          </span>
        ),
      },
      {
        id: "channel",
        header: "Channel",
        enableSorting: false,
        cell: ({ row }) => <Badge>{humanizeCode(row.original.channel)}</Badge>,
      },
      {
        id: "isActive",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => <StatusBadge code={String(row.original.isActive)} map={ACTIVE_FLAG} />,
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => (
          <Button
            size="sm"
            variant="ghost"
            aria-label={`Edit ${humanizeCode(row.original.code)} ${humanizeCode(row.original.channel)}`}
            onClick={() => setEditing(row.original)}
          >
            Edit
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <PageHeader
        title="Notification templates"
        description="Messages customers and staff receive. Placeholders in {{double braces}} are filled by the server."
      />
      <DataTable
        caption="Notification templates"
        columns={columns}
        data={rows}
        getRowId={(t) => t.templateId}
        getRowLabel={(t) => `${t.code} ${t.channel}`}
        page={1}
        pageSize={Math.max(rows.length, 1)}
        totalCount={rows.length}
        onPageChange={() => undefined}
        isLoading={templates.isFetching}
        error={tableError(templates.error)}
        onRetry={() => void templates.refetch()}
        emptyState={<EmptyState icon={<Mail aria-hidden="true" />} title="No templates" />}
      />
      {editing && (
        <TemplateDialog
          key={editing.templateId}
          template={editing}
          onClose={() => setEditing(null)}
          onConflict={() => {
            setEditing(null);
            void templates.refetch();
          }}
        />
      )}
    </>
  );
}

const schema = z.object({
  subject: z.string(),
  body: z.string().trim().min(1, "Enter the message text."),
  isActive: z.boolean(),
});
type Values = z.infer<typeof schema>;

function TemplateDialog({
  template,
  onClose,
  onConflict,
}: {
  template: NotificationTemplate;
  onClose: () => void;
  onConflict: () => void;
}) {
  const update = useUpdateTemplate();
  const test = useSendTestTemplate();
  const hasSubject = template.subject !== null || template.channel === "EMAIL";
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      subject: template.subject ?? "",
      body: template.body,
      isActive: template.isActive,
    },
  });

  const onSubmit = form.handleSubmit((v) =>
    update.mutate(
      {
        code: template.code,
        channel: template.channel,
        version: template.version,
        body: {
          subject: hasSubject ? v.subject.trim() || null : null,
          body: v.body,
          isActive: v.isActive,
        },
      },
      {
        onSuccess: () => {
          notifySaved("Template saved");
          onClose();
        },
        onError: (error) => {
          // An unknown placeholder belongs to the body even when the server names no field.
          if (
            isApiError(error) &&
            error.code === "TEMPLATE_PLACEHOLDER_UNKNOWN" &&
            Object.keys(error.errors).length === 0
          ) {
            form.setError("body", { type: error.code, message: errorMessage(error) });
            return;
          }
          reportError(error, {
            setError: form.setError,
            fields: ["subject", "body", "isActive"],
            onConflict,
          });
        },
      },
    ),
  );

  const sendTest = () =>
    test.mutate(
      { code: template.code, channel: template.channel },
      {
        onSuccess: (result) =>
          result.sent
            ? notifySaved("Test sent", `Sent to ${result.recipient ?? "your account"}.`)
            : notifySaved(
                "Test rendered, not sent",
                "Your account has no address for this channel.",
              ),
        onError: (error) => reportError(error, { title: "Could not send the test" }),
      },
    );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title={`${humanizeCode(template.code)} · ${humanizeCode(template.channel)}`}
        description={template.dltTemplateId ? `DLT template ${template.dltTemplateId}` : undefined}
        footer={
          <>
            <Button variant="outline" loading={test.isPending} onClick={sendTest}>
              Send test to me
            </Button>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={update.isPending} onClick={() => void onSubmit()}>
              Save
            </Button>
          </>
        }
      >
        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit();
          }}
        >
          {hasSubject && <TextField form={form} name="subject" label="Subject" />}
          <TextAreaField
            form={form}
            name="body"
            label="Message"
            rows={8}
            required
            hint={
              template.variables.length > 0
                ? `Placeholders: ${template.variables.map((v) => `{{${v}}}`).join(", ")}`
                : "This template takes no placeholders."
            }
          />
          <Controller
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <Switch
                label="Active"
                description="Inactive templates are not sent."
                checked={field.value}
                onCheckedChange={field.onChange}
              />
            )}
          />
        </form>
      </DialogContent>
    </Dialog>
  );
}
