import { PageBand, Script } from "@fresh/ui";
import type { Metadata } from "next";
import { type ReactNode } from "react";
import { AccountNav } from "@/components/account/account-nav";
import { RequireAuth } from "@/components/auth/require-auth";
import { Container } from "@/components/section";

export const metadata: Metadata = {
  title: { template: "%s · Account · Fresh Orbitt", default: "Account" },
  robots: { index: false },
};

/**
 * Account pages (D95): the coral band, the section chips and the page. Per customer and client-rendered behind the
 * in-memory session. Each page's own title is its h1 (the band's heading is an h2).
 */
export default function AccountLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <PageBand
        headingLevel={2}
        title={
          <>
            Your <Script>account</Script>
          </>
        }
      />
      <Container className="flex flex-col gap-6 py-6 sm:py-8">
        <AccountNav />
        <div className="min-w-0">
          <RequireAuth>{children}</RequireAuth>
        </div>
      </Container>
    </>
  );
}
