"use client";

import { Spinner } from "@fresh/ui";
import { usePathname, useRouter } from "next/navigation";
import { type ReactNode, useEffect } from "react";
import { retrySession } from "@/auth/session-store";
import { useSession } from "@/auth/use-session";
import { LoadError } from "@/components/states";

/** How long the page waits before trying to restore an unreachable session again on its own. */
const AUTO_RETRY_MS = 10_000;

/**
 * Renders children for a signed-in customer; sends a signed-out visitor to sign in and back. When the shop could
 * not be reached to restore the session (`unavailable`), the customer stays signed in: the page says so, offers
 * Try again and retries on its own (W-6, D317).
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useSession();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status === "signedOut") router.replace(`/login?next=${encodeURIComponent(pathname)}`);
  }, [status, router, pathname]);

  useEffect(() => {
    if (status !== "unavailable") return;
    const timer = window.setTimeout(() => void retrySession(), AUTO_RETRY_MS);
    return () => window.clearTimeout(timer);
  }, [status]);

  if (status === "signedIn") return <>{children}</>;
  if (status === "unavailable") {
    return (
      <div className="py-12">
        <LoadError
          title="We could not reach the shop"
          description="You are still signed in. Please try again in a moment."
          error={null}
          slot="OFFLINE"
          onRetry={() => void retrySession()}
        />
      </div>
    );
  }
  return (
    <div className="text-text-muted flex items-center justify-center gap-3 py-24" role="status">
      <Spinner />
      <span>{status === "signedOut" ? "Taking you to sign in…" : "Checking your session…"}</span>
    </div>
  );
}
