import { zodResolver } from "@hookform/resolvers/zod";
import { isApiError } from "@fresh/core";
import { Badge, Button } from "@fresh/ui";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useChangePassword, useMe } from "../../api/auth";
import { useLocationScope } from "../../app/location-scope";
import { TextField } from "../../components/form";
import { DetailList, PageHeader, QueryView, Section } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved, reportError } from "../../lib/feedback";

const schema = z
  .object({
    currentPassword: z.string().min(1, "Enter your current password."),
    newPassword: z.string().min(1, "Enter a new password."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "Type the same new password again.",
  });
type Values = z.infer<typeof schema>;

export function AccountPage() {
  const me = useMe();
  const { nameOf } = useLocationScope();
  return (
    <>
      <PageHeader title="My account" />
      <div className="flex flex-col gap-4">
        <QueryView query={me} what="profile">
          {(profile) => (
            <Section title="Profile">
              <DetailList
                items={[
                  { label: "Name", value: profile.fullName },
                  { label: "Email", value: profile.email },
                  {
                    label: "Roles",
                    value: (
                      <span className="flex flex-wrap gap-1">
                        {profile.roles.map((r) => (
                          <Badge key={r.id}>{r.name}</Badge>
                        ))}
                      </span>
                    ),
                  },
                  {
                    label: "Locations",
                    value: profile.allLocations
                      ? "All locations"
                      : profile.locationIds.map((id) => nameOf(id)).join(", ") || "—",
                  },
                ]}
              />
            </Section>
          )}
        </QueryView>
        <ChangePassword />
      </div>
    </>
  );
}

function ChangePassword() {
  const change = useChangePassword();
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const onSubmit = form.handleSubmit((v) =>
    change.mutate(
      { currentPassword: v.currentPassword, newPassword: v.newPassword },
      {
        onSuccess: () => {
          form.reset();
          notifySaved("Password changed", "Sign in again with the new password when asked.");
        },
        onError: (error) => {
          // These refusals belong to one field even when the response names none.
          const code =
            isApiError(error) && Object.keys(error.errors).length === 0 ? error.code : null;
          if (code === "INVALID_CREDENTIALS") {
            form.setError(
              "currentPassword",
              { type: code, message: "Your current password is not correct." },
              { shouldFocus: true },
            );
            return;
          }
          if (code === "PASSWORD_TOO_WEAK") {
            form.setError(
              "newPassword",
              { type: code, message: errorMessage(error) },
              { shouldFocus: true },
            );
            return;
          }
          reportError(error, {
            setError: form.setError,
            fields: ["currentPassword", "newPassword"],
            title: "Could not change the password",
          });
        },
      },
    ),
  );

  return (
    <Section title="Change password" description="Changing it signs you out on every device.">
      <form
        className="flex max-w-md flex-col gap-4"
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit();
        }}
      >
        <TextField
          form={form}
          name="currentPassword"
          label="Current password"
          type="password"
          autoComplete="current-password"
          required
        />
        <TextField
          form={form}
          name="newPassword"
          label="New password"
          type="password"
          autoComplete="new-password"
          required
        />
        <TextField
          form={form}
          name="confirmPassword"
          label="Repeat new password"
          type="password"
          autoComplete="new-password"
          required
        />
        <div>
          <Button type="submit" loading={change.isPending}>
            Change password
          </Button>
        </div>
      </form>
    </Section>
  );
}
