import type { Metadata } from "next";
import { type ReactNode } from "react";
import { AccountNav } from "@/components/account/account-nav";
import { RequireAuth } from "@/components/auth/require-auth";
import { Container } from "@/components/section";

export const metadata: Metadata = {
  title: { template: "%s · Account · Fresh Orbit", default: "Account" },
  robots: { index: false },
};

/** Account pages are per customer and client-rendered behind the in-memory session. */
export default function AccountLayout({ children }: { children: ReactNode }) {
  return (
    <Container className="grid gap-8 py-8 md:grid-cols-[14rem_1fr]">
      <AccountNav />
      <div className="min-w-0">
        <RequireAuth>{children}</RequireAuth>
      </div>
    </Container>
  );
}
