import { zodResolver } from "@hookform/resolvers/zod";
import { Badge, Button } from "@fresh/ui";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useBusinessProfile, useSaveBusinessProfile } from "../../api/settings";
import { type BusinessProfile } from "../../api/types/settings";
import { TextAreaField, TextField } from "../../components/form";
import { PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";

const schema = z.object({
  legalName: z.string().trim().min(1, "Enter the legal name."),
  tradeName: z.string().trim().min(1, "Enter the trade name customers see."),
  gstin: z.string().trim(),
  fssaiLicenseNo: z.string().trim(),
  address: z.string().trim().min(1, "Enter the registered address."),
  supportPhone: z
    .string()
    .trim()
    .regex(/^\+[1-9]\d{6,14}$/, "Enter the support phone with country code, e.g. +919876543210."),
  supportEmail: z.string().trim().pipe(z.email("Enter a valid support email address.")),
  timeZone: z.string().trim().min(1, "Enter the time zone, e.g. Asia/Kolkata."),
});
type Values = z.infer<typeof schema>;
const FIELDS = Object.keys(schema.shape);

export function BusinessProfilePage() {
  const profile = useBusinessProfile();
  return (
    <>
      <PageHeader
        title="Business profile"
        description="Printed on invoices and shown to customers."
        meta={profile.data === null ? <Badge tone="warning">Not set up</Badge> : undefined}
      />
      <QueryView query={profile} what="business profile">
        {(data) => (
          <ProfileForm
            key={data?.version ?? "new"}
            profile={data}
            onConflict={() => profile.refetch()}
          />
        )}
      </QueryView>
    </>
  );
}

function ProfileForm({
  profile,
  onConflict,
}: {
  profile: BusinessProfile | null;
  onConflict: () => unknown;
}) {
  const save = useSaveBusinessProfile();
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      legalName: profile?.legalName ?? "",
      tradeName: profile?.tradeName ?? "",
      gstin: profile?.gstin ?? "",
      fssaiLicenseNo: profile?.fssaiLicenseNo ?? "",
      address: profile?.address ?? "",
      supportPhone: profile?.supportPhone ?? "",
      supportEmail: profile?.supportEmail ?? "",
      timeZone: profile?.timeZone ?? "Asia/Kolkata",
    },
  });

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(
      {
        body: { ...v, gstin: v.gstin || null, fssaiLicenseNo: v.fssaiLicenseNo || null },
        version: profile?.version ?? null,
      },
      {
        onSuccess: () => notifySaved("Business profile saved"),
        onError: (error) =>
          reportError(error, { setError: form.setError, fields: FIELDS, onConflict }),
      },
    ),
  );

  return (
    <form
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        void onSubmit();
      }}
    >
      <Section
        title="Company"
        description={
          profile ? undefined : "No profile is saved yet. Invoices cannot be issued until it is."
        }
      >
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <TextField form={form} name="legalName" label="Legal name" required />
          <TextField form={form} name="tradeName" label="Trade name" required />
          <TextField form={form} name="gstin" label="GSTIN" hint="15 characters." />
          <TextField form={form} name="fssaiLicenseNo" label="FSSAI licence number" />
          <TextAreaField
            form={form}
            name="address"
            label="Registered address"
            required
            className="md:col-span-2"
          />
          <TextField form={form} name="supportPhone" label="Support phone" type="tel" required />
          <TextField form={form} name="supportEmail" label="Support email" type="email" required />
          <TextField form={form} name="timeZone" label="Time zone" required />
        </div>
        <div className="flex justify-end">
          <Button type="submit" loading={save.isPending}>
            {profile ? "Save" : "Create profile"}
          </Button>
        </div>
      </Section>
    </form>
  );
}
