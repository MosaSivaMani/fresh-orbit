import { zodResolver } from "@hookform/resolvers/zod";
import { isApiError } from "@fresh/core";
import { Button, Checkbox, Dialog, DialogContent, FormField, Switch } from "@fresh/ui";
import { useState } from "react";
import {
  Controller,
  type FieldValues,
  type UseFormReturn,
  useForm,
  useWatch,
} from "react-hook-form";
import { z } from "zod";
import { useCreateStaff, useResetStaffPassword, useRoles, useUpdateStaff } from "../../api/staff";
import { type Staff } from "../../api/types/staff";
import { useLocationScope } from "../../app/location-scope";
import { TextField } from "../../components/form";
import { applyFieldErrors, errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";
import { FormAlert } from "./shared";

const staffSchema = z
  .object({
    email: z.string().trim(),
    fullName: z.string().trim().min(1, "Enter the staff member's full name."),
    roleIds: z.array(z.string()).min(1, "Choose at least one role."),
    allLocations: z.boolean(),
    locationIds: z.array(z.string()),
    isActive: z.boolean(),
    temporaryPassword: z.string(),
    creating: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (v.creating && !z.email().safeParse(v.email).success)
      ctx.addIssue({ code: "custom", path: ["email"], message: "Enter a valid email address." });
    if (v.creating && !v.temporaryPassword)
      ctx.addIssue({
        code: "custom",
        path: ["temporaryPassword"],
        message: "Enter a temporary password.",
      });
    if (!v.allLocations && v.locationIds.length === 0)
      ctx.addIssue({
        code: "custom",
        path: ["locationIds"],
        message: "Choose at least one location, or give access to all locations.",
      });
  });

type StaffValues = z.infer<typeof staffSchema>;
const STAFF_FIELDS = [
  "email",
  "fullName",
  "roleIds",
  "allLocations",
  "locationIds",
  "isActive",
  "temporaryPassword",
] as const;

/** Server refusals that belong to the password field even when the response names no field. */
function applyPasswordError<T extends FieldValues>(
  error: unknown,
  form: UseFormReturn<T>,
  field: string,
): boolean {
  if (isApiError(error) && error.code === "PASSWORD_TOO_WEAK" && !error.errors[field]) {
    form.setError(field as never, { type: error.code, message: errorMessage(error) });
    return true;
  }
  return false;
}

/** Create (no `staff`) or edit a staff member: roles, locations, active flag. */
export function StaffFormDialog({ staff, onClose }: { staff?: Staff; onClose: () => void }) {
  const creating = !staff;
  const roles = useRoles();
  const { locations } = useLocationScope();
  const create = useCreateStaff();
  const update = useUpdateStaff();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<StaffValues>({
    resolver: zodResolver(staffSchema),
    defaultValues: {
      email: staff?.email ?? "",
      fullName: staff?.fullName ?? "",
      roleIds: staff?.roles.map((r) => r.id) ?? [],
      allLocations: staff?.allLocations ?? false,
      locationIds: staff?.locationIds ?? [],
      isActive: staff?.isActive ?? true,
      temporaryPassword: "",
      creating,
    },
  });
  const allLocations = useWatch({ control: form.control, name: "allLocations" });

  // Locations the signed-in manager can see, plus any the edited staff member already has.
  const locationChoices = [
    ...locations,
    ...(staff?.locationIds ?? [])
      .filter((id) => !locations.some((l) => l.id === id))
      .map((id) => ({ id, name: `Location ${id.slice(-6)}`, code: null })),
  ];

  const onError = (error: unknown) => {
    const unmatched = applyFieldErrors(error, form.setError, STAFF_FIELDS);
    if (applyPasswordError(error, form, "temporaryPassword")) return;
    if (isApiError(error) && error.code === "CONCURRENCY_CONFLICT") {
      reportError(error, { onConflict: onClose });
      return;
    }
    if (!isApiError(error) || Object.keys(error.errors).length === 0)
      setFormError(errorMessage(error));
    else if (unmatched.length > 0) setFormError(unmatched.join(" "));
  };

  const onSubmit = form.handleSubmit(async (v) => {
    setFormError(null);
    const locationIds = v.allLocations ? null : v.locationIds;
    try {
      if (staff) {
        await update.mutateAsync({
          id: staff.id,
          version: staff.version,
          body: {
            fullName: v.fullName,
            roleIds: v.roleIds,
            allLocations: v.allLocations,
            locationIds,
            isActive: v.isActive,
          },
        });
        notifySaved("Staff member saved");
      } else {
        await create.mutateAsync({
          email: v.email,
          fullName: v.fullName,
          roleIds: v.roleIds,
          allLocations: v.allLocations,
          locationIds,
          temporaryPassword: v.temporaryPassword,
        });
        notifySaved("Staff member created", "Share the temporary password with them privately.");
      }
      onClose();
    } catch (error) {
      onError(error);
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title={staff ? `Edit ${staff.fullName}` : "New staff member"}
        description={
          creating
            ? "They sign in with this email and must change the temporary password."
            : undefined
        }
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={create.isPending || update.isPending} onClick={() => void onSubmit()}>
              {creating ? "Create staff member" : "Save"}
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
          {creating && (
            <TextField
              form={form}
              name="email"
              label="Email"
              type="email"
              required
              autoComplete="off"
            />
          )}
          <TextField form={form} name="fullName" label="Full name" required />
          <FormField label="Roles" required error={form.formState.errors.roleIds?.message}>
            <Controller
              control={form.control}
              name="roleIds"
              render={({ field }) => (
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {(roles.data ?? []).map((role) => (
                    <Checkbox
                      key={role.id}
                      label={role.name}
                      checked={field.value.includes(role.id)}
                      onCheckedChange={(checked) =>
                        field.onChange(
                          checked === true
                            ? [...field.value, role.id]
                            : field.value.filter((id) => id !== role.id),
                        )
                      }
                    />
                  ))}
                </div>
              )}
            />
          </FormField>
          <Controller
            control={form.control}
            name="allLocations"
            render={({ field }) => (
              <Switch
                label="All locations"
                description="Includes locations opened later."
                checked={field.value}
                onCheckedChange={field.onChange}
              />
            )}
          />
          {!allLocations && (
            <FormField
              label="Locations"
              required
              error={form.formState.errors.locationIds?.message}
            >
              <Controller
                control={form.control}
                name="locationIds"
                render={({ field }) => (
                  <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    {locationChoices.map((l) => (
                      <Checkbox
                        key={l.id}
                        label={l.name}
                        checked={field.value.includes(l.id)}
                        onCheckedChange={(checked) =>
                          field.onChange(
                            checked === true
                              ? [...field.value, l.id]
                              : field.value.filter((id) => id !== l.id),
                          )
                        }
                      />
                    ))}
                  </div>
                )}
              />
            </FormField>
          )}
          {creating ? (
            <TextField
              form={form}
              name="temporaryPassword"
              label="Temporary password"
              type="password"
              autoComplete="new-password"
              required
            />
          ) : (
            <Controller
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <Switch
                  label="Active"
                  description="Inactive staff cannot sign in."
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              )}
            />
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}

const resetSchema = z.object({
  temporaryPassword: z.string().min(1, "Enter a temporary password."),
});
type ResetValues = z.infer<typeof resetSchema>;

export function ResetPasswordDialog({ staff, onClose }: { staff: Staff; onClose: () => void }) {
  const reset = useResetStaffPassword();
  const form = useForm<ResetValues>({
    resolver: zodResolver(resetSchema),
    defaultValues: { temporaryPassword: "" },
  });
  const onSubmit = form.handleSubmit(async (v) => {
    try {
      await reset.mutateAsync({ id: staff.id, version: staff.version, body: v });
      notifySaved(
        "Password reset",
        "Their sessions were signed out. Share the new password privately.",
      );
      onClose();
    } catch (error) {
      if (applyPasswordError(error, form, "temporaryPassword")) return;
      reportError(error, {
        setError: form.setError,
        fields: ["temporaryPassword"],
        onConflict: onClose,
        title: "Could not reset the password",
      });
    }
  });
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={`Reset password for ${staff.fullName}`}
        description="They are signed out everywhere and must choose a new password at next sign-in."
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={reset.isPending} onClick={() => void onSubmit()}>
              Reset password
            </Button>
          </>
        }
      >
        <form
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            void onSubmit();
          }}
        >
          <TextField
            form={form}
            name="temporaryPassword"
            label="Temporary password"
            type="password"
            autoComplete="new-password"
            required
          />
        </form>
      </DialogContent>
    </Dialog>
  );
}
