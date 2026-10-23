"use client";

import { Spinner } from "@fresh/ui";
import { usePathname, useRouter } from "next/navigation";
import { type ReactNode, useEffect } from "react";
import { useSession } from "@/auth/use-session";

/** Renders children for a signed-in customer; otherwise sends the visitor to sign in and back. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useSession();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status === "signedOut") router.replace(`/login?next=${encodeURIComponent(pathname)}`);
  }, [status, router, pathname]);

  if (status === "signedIn") return <>{children}</>;
  return (
    <div className="text-text-muted flex items-center justify-center gap-3 py-24" role="status">
      <Spinner />
      <span>{status === "signedOut" ? "Taking you to sign in…" : "Checking your session…"}</span>
    </div>
  );
}
