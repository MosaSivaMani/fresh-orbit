import { zodResolver } from "@hookform/resolvers/zod";
import { isApiError } from "@fresh/core";
import {
  Badge,
  Button,
  Checkbox,
  Dialog,
  DialogContent,
  EmptyState,
  ErrorState,
  FormField,
  Skeleton,
} from "@fresh/ui";
import { Plus, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import {
  useCreateRole,
  useDeleteRole,
  usePermissionCatalogue,
  useRoles,
  useUpdateRole,
} from "../../api/staff";
import { type Role } from "../../api/types/staff";
import { Section } from "../../components/page";
import { TextField } from "../../components/form";
import { applyFieldErrors, errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { FormAlert } from "./shared";

type Dialog = { kind: "create" } | { kind: "edit"; role: Role } | { kind: "delete"; role: Role };

export function RolesPanel() {
  const roles = useRoles();
  const [dialog, setDialog] = useState<Dialog | null>(null);

  if (roles.isPending) return <Skeleton className="h-40 w-full" />;
  if (roles.error)
    return (
      <ErrorState
        title="Could not load roles"
        traceId={isApiError(roles.error) ? roles.error.traceId : undefined}
        onRetry={() => void roles.refetch()}
      />
    );

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button
          size="sm"
          leadingIcon={<Plus aria-hidden="true" />}
          onClick={() => setDialog({ kind: "create" })}
        >
          New role
        </Button>
      </div>
      {roles.data.length === 0 && (
        <EmptyState icon={<ShieldCheck aria-hidden="true" />} title="No roles yet" />
      )}
      {roles.data.map((role) => (
        <Section
          key={role.id}
          title={role.name}
          description={role.code}
          actions={
            role.isSystem ? (
              <Badge tone="info">Built-in</Badge>
            ) : (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  aria-label={`Edit role ${role.name}`}
                  onClick={() => setDialog({ kind: "edit", role })}
                >
                  Edit
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label={`Delete role ${role.name}`}
                  onClick={() => setDialog({ kind: "delete", role })}
                >
                  Delete
                </Button>
              </>
            )
          }
        >
          <div className="flex flex-wrap gap-1">
            {role.permissions.length === 0 ? (
              <span className="text-text-muted text-sm">No permissions</span>
            ) : (
              role.permissions.map((p) => <Badge key={p}>{p}</Badge>)
            )}
          </div>
        </Section>
      ))}
      {dialog?.kind === "create" && <RoleFormDialog onClose={() => setDialog(null)} />}
      {dialog?.kind === "edit" && (
        <RoleFormDialog role={dialog.role} onClose={() => setDialog(null)} />
      )}
      {dialog?.kind === "delete" && (
        <DeleteRoleDialog role={dialog.role} onClose={() => setDialog(null)} />
      )}
    </div>
  );
}

const roleSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^[A-Z][A-Z0-9_]*$/, "Use capital letters, digits and underscores, e.g. NIGHT_PACKER."),
  name: z.string().trim().min(1, "Enter the role name."),
  permissions: z.array(z.string()),
});
type RoleValues = z.infer<typeof roleSchema>;

function RoleFormDialog({ role, onClose }: { role?: Role; onClose: () => void }) {
  const catalogue = usePermissionCatalogue();
  const create = useCreateRole();
  const update = useUpdateRole();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<RoleValues>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      code: role?.code ?? "",
      name: role?.name ?? "",
      permissions: role?.permissions ?? [],
    },
  });

  const onSubmit = form.handleSubmit(async (v) => {
    setFormError(null);
    try {
      if (role) {
        await update.mutateAsync({
          id: role.id,
          version: role.version,
          body: { name: v.name, permissions: v.permissions },
        });
      } else {
        await create.mutateAsync(v);
      }
      notifySaved(role ? "Role saved" : "Role created");
      onClose();
    } catch (error) {
      if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT") {
        reportError(error, { onConflict: onClose });
        return;
      }
      const unmatched = applyFieldErrors(error, form.setError, ["code", "name", "permissions"]);
      if (!isApiError(error) || Object.keys(error.errors).length === 0)
        setFormError(errorMessage(error));
      else if (unmatched.length > 0) setFormError(unmatched.join(" "));
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title={role ? `Edit ${role.name}` : "New role"}
        description="Staff with this role get every permission ticked here."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={create.isPending || update.isPending} onClick={() => void onSubmit()}>
              {role ? "Save" : "Create role"}
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
          <FormAlert message={formError} />
          {!role && <TextField form={form} name="code" label="Code" required />}
          <TextField form={form} name="name" label="Name" required />
          <FormField label="Permissions" error={form.formState.errors.permissions?.message}>
            {catalogue.isPending ? (
              <Skeleton className="h-32 w-full" />
            ) : catalogue.error ? (
              <p className="text-danger-text text-sm">{errorMessage(catalogue.error)}</p>
            ) : (
              <Controller
                control={form.control}
                name="permissions"
                render={({ field }) => (
                  <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    {catalogue.data.map((p) => (
                      <Checkbox
                        key={p.code}
                        label={p.code}
                        description={p.description}
                        checked={field.value.includes(p.code)}
                        onCheckedChange={(checked) =>
                          field.onChange(
                            checked === true
                              ? [...field.value, p.code]
                              : field.value.filter((c) => c !== p.code),
                          )
                        }
                      />
                    ))}
                  </div>
                )}
              />
            )}
          </FormField>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function DeleteRoleDialog({ role, onClose }: { role: Role; onClose: () => void }) {
  const remove = useDeleteRole();
  const [formError, setFormError] = useState<string | null>(null);
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={`Delete ${role.name}?`}
        description="A role still given to staff cannot be deleted."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Keep role
            </Button>
            <Button
              variant="danger"
              loading={remove.isPending}
              onClick={() =>
                remove.mutate(
                  { id: role.id, version: role.version },
                  {
                    onSuccess: () => {
                      notifySaved("Role deleted");
                      onClose();
                    },
                    onError: (error) => {
                      if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT")
                        reportError(error, { onConflict: onClose });
                      else setFormError(errorMessage(error));
                    },
                  },
                )
              }
            >
              Delete role
            </Button>
          </>
        }
      >
        <FormAlert message={formError} />
      </DialogContent>
    </Dialog>
  );
}
