"use client";

import {
  Button,
  Card,
  CardContent,
  Checkbox,
  Dialog,
  DialogContent,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  toast,
} from "@fresh/ui";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { type FormEvent, useState } from "react";
import { browserHttp } from "@/api/browser";
import { deleteAccount, getProfile, updateProfile } from "@/api/endpoints/account";
import { type Profile } from "@/api/types/account";
import { signOut } from "@/auth/session-store";
import { asApiError, describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

function ProfileForm({ profile }: { profile: Profile }) {
  const queryClient = useQueryClient();
  const [fullName, setFullName] = useState(profile.fullName ?? "");
  const [email, setEmail] = useState(profile.email ?? "");
  const [marketing, setMarketing] = useState(profile.marketingOptIn);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function save(event: FormEvent) {
    event.preventDefault();
    setErrors({});
    setFormError(null);
    setPending(true);
    try {
      const updated = await updateProfile(
        browserHttp(),
        {
          fullName: fullName.trim() || null,
          email: email.trim() || null,
          marketingOptIn: marketing,
        },
        profile.version,
      );
      queryClient.setQueryData(queryKeys.profile, updated);
      toast({ title: "Profile saved", tone: "success" });
    } catch (e) {
      if (hasCode(e, "CONCURRENCY_CONFLICT")) {
        setFormError("Your profile was changed elsewhere. We have loaded the latest version.");
        void queryClient.invalidateQueries({ queryKey: queryKeys.profile });
        return;
      }
      const fields: Record<string, string> = {};
      for (const f of ["fullName", "email", "marketingOptIn"]) {
        const m = fieldMessage(e, f);
        if (m) fields[f] = m;
      }
      setErrors(fields);
      if (!Object.keys(fields).length) setFormError(describeError(e));
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(e) => void save(e)} noValidate className="flex flex-col gap-4">
      <FormField label="Mobile number" hint="Your sign-in number cannot be changed here.">
        <Input value={profile.phone ?? ""} readOnly disabled />
      </FormField>
      <FormField label="Full name" error={errors.fullName}>
        <Input
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          autoComplete="name"
          maxLength={120}
        />
      </FormField>
      <FormField label="Email (optional)" error={errors.email} hint="For invoices and receipts.">
        <Input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          autoComplete="email"
        />
      </FormField>
      <Checkbox
        label="Send me offers and news"
        checked={marketing}
        onCheckedChange={(c) => setMarketing(c === true)}
      />
      {formError && (
        <p role="alert" className="text-danger-text text-sm">
          {formError}
        </p>
      )}
      <Button type="submit" className="self-start" loading={pending}>
        Save changes
      </Button>
    </form>
  );
}

function DeleteAccount({ profile }: { profile: Profile }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      await deleteAccount(browserHttp(), profile.version);
      await signOut();
      router.replace("/");
    } catch (e) {
      setError(describeError(e));
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <Button variant="outline" className="self-start" onClick={() => setOpen(true)}>
        Delete my account
      </Button>
      <DialogContent
        title="Delete your account?"
        description="Your name, email, addresses and saved items are removed and you are signed out everywhere. This cannot be undone."
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Keep account
            </Button>
            <Button
              variant="danger"
              disabled={confirmText !== "DELETE"}
              loading={pending}
              onClick={() => void confirm()}
            >
              Delete account
            </Button>
          </>
        }
      >
        <FormField label='Type "DELETE" to confirm'>
          <Input value={confirmText} onChange={(e) => setConfirmText(e.target.value)} />
        </FormField>
        {error && (
          <p role="alert" className="text-danger-text text-sm">
            {error}
          </p>
        )}
      </DialogContent>
    </Dialog>
  );
}

export function ProfileView() {
  const router = useRouter();
  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: () => getProfile(browserHttp()),
  });

  if (profile.isPending) return <Skeleton className="h-80 rounded-xl" />;
  if (profile.isError) {
    return (
      <ErrorState
        title="We could not load your profile"
        description={describeError(profile.error)}
        traceId={asApiError(profile.error)?.traceId}
        onRetry={() => void profile.refetch()}
      />
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-text text-2xl font-bold">Profile</h1>
      <Card>
        <CardContent>
          {/* Keyed by version so a reload after a conflict resets the form to the server's values. */}
          <ProfileForm key={profile.data.version} profile={profile.data} />
        </CardContent>
      </Card>
      <Card>
        <CardContent className="flex flex-col gap-3">
          <h2 className="text-text text-lg font-semibold">Session</h2>
          <Button
            variant="secondary"
            className="self-start"
            onClick={() => void signOut().then(() => router.replace("/"))}
          >
            Sign out
          </Button>
        </CardContent>
      </Card>
      <Card>
        <CardContent className="flex flex-col gap-3">
          <h2 className="text-text text-lg font-semibold">Delete account</h2>
          <p className="text-text-muted text-sm">
            Deleting is refused while an order is still in progress.
          </p>
          <DeleteAccount profile={profile.data} />
        </CardContent>
      </Card>
    </div>
  );
}
