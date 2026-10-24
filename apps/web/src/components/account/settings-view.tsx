"use client";

import { Button, Skeleton, StepCard } from "@fresh/ui";
import { useQuery } from "@tanstack/react-query";
import { LogOut } from "lucide-react";
import { useRouter } from "next/navigation";
import { browserHttp } from "@/api/browser";
import { getProfile } from "@/api/endpoints/account";
import { signOut } from "@/auth/session-store";
import { LoadError } from "@/components/states";
import { queryKeys } from "@/lib/query-keys";
import { Preferences } from "./notifications-view";
import { DeleteAccount } from "./profile-view";

/** Settings (D95): how we contact you, signing out, and deleting the account (`If-Match` with the profile version). */
export function SettingsView() {
  const router = useRouter();
  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: () => getProfile(browserHttp()),
  });

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <h1 className="font-display text-text text-3xl font-extrabold">Settings</h1>
      <StepCard title="How we contact you">
        <Preferences />
      </StepCard>
      <StepCard title="Session">
        <p className="text-text-muted text-sm">Signs you out on this browser.</p>
        <Button
          variant="outline"
          className="self-start"
          leadingIcon={<LogOut aria-hidden="true" />}
          onClick={() => void signOut().then(() => router.replace("/"))}
        >
          Sign out
        </Button>
      </StepCard>
      <StepCard title="Delete account">
        <p className="text-text-muted text-sm">
          Deleting is refused while an order is still in progress.
        </p>
        {profile.isPending ? (
          <Skeleton className="h-10 w-44" />
        ) : profile.isError ? (
          <LoadError
            title="We could not load your account"
            error={profile.error}
            headingLevel={3}
            onRetry={() => void profile.refetch()}
          />
        ) : (
          <DeleteAccount profile={profile.data} />
        )}
      </StepCard>
    </div>
  );
}
