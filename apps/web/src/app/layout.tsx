import type { Metadata, Viewport } from "next";
import { Inter, Plus_Jakarta_Sans } from "next/font/google";
import { type ReactNode } from "react";
import { SiteHeader } from "@/components/header/site-header";
import { SiteFooter } from "@/components/site-footer";
import { getSiteUrl } from "@/server/config";
import { Providers } from "./providers";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" });
const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  variable: "--font-jakarta",
  display: "swap",
  weight: ["600", "700"],
});

const siteUrl = getSiteUrl();

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
    <html lang="en-IN" className={`${inter.variable} ${jakarta.variable}`}>
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
