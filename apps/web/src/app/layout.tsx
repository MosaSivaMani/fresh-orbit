import type { Metadata, Viewport } from "next";
import { type ReactNode } from "react";
import { SiteHeader } from "@/components/header/site-header";
import { SiteFooter } from "@/components/site-footer";
import { getSiteUrl } from "@/server/config";
import { Providers } from "./providers";
import "./globals.css";

// Fonts (Baloo 2, Inter, Dancing Script) are self-hosted from @fresh/ui/fonts.css, imported by globals.css (D89).

const siteUrl = getSiteUrl();

// Static pages (about, support, policies, account shells) are regenerated at most every 5 minutes, so the footer's
// city list, read from the API, catches up with a newly activated city (E2E-6) and with a build that ran without the
// API. Next.js needs a literal here: keep it equal to FOOTER_CITIES_REVALIDATE_SECONDS (site-footer.tsx).
export const revalidate = 300;

export const metadata: Metadata = {
  ...(siteUrl ? { metadataBase: new URL(siteUrl) } : {}),
  title: {
    default: "Fresh Orbit — fresh fish and seafood, cleaned and delivered",
    template: "%s · Fresh Orbit",
  },
  description:
    "Order fresh fish, prawns, crabs and shellfish, cleaned and cut the way you like. Pay for net weight after cleaning.",
  applicationName: "Fresh Orbit",
  openGraph: { type: "website", siteName: "Fresh Orbit", locale: "en_IN" },
  formatDetection: { telephone: false },
};

export const viewport: Viewport = {
  themeColor: "#F07A5A",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en-IN">
      <body className="flex min-h-dvh flex-col">
        <a
          href="#main"
          className="bg-primary text-on-primary sr-only z-[1600] rounded-md px-4 py-2 focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
        >
          Skip to content
        </a>
        <Providers>
          <SiteHeader />
          <main id="main" className="flex-1">
            {children}
          </main>
          <SiteFooter />
        </Providers>
      </body>
    </html>
  );
}
