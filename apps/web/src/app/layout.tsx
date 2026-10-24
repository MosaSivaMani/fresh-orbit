import type { Metadata, Viewport } from "next";
import { type ReactNode } from "react";
import { CartBar } from "@/components/header/cart-bar";
import { SiteHeader } from "@/components/header/site-header";
import { AppTabBar } from "@/components/header/tab-bar";
import { SiteFooter } from "@/components/site-footer";
import { getSiteUrl } from "@/server/config";
import { Providers } from "./providers";
import "./globals.css";

// Fonts (Baloo 2, Inter, Dancing Script) are self-hosted from @fresh/ui/fonts.css, imported by globals.css (D89).

const siteUrl = getSiteUrl();

// Static pages (about, support, policies, account shells) are regenerated at most hourly so the footer's city list,
// read from the API, catches up even when the build ran without the API.
export const revalidate = 3600;

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
  themeColor: "#073B4C",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en-IN">
      {/* Bottom padding keeps the footer clear of the phone tab bar (hidden from md up). */}
      <body className="flex min-h-dvh flex-col pb-[76px] md:pb-0">
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
          <CartBar />
          <AppTabBar />
        </Providers>
      </body>
    </html>
  );
}
