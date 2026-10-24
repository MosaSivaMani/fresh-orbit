import Link from "next/link";
import { Suspense } from "react";
import { Logo } from "../logo";
import { DesktopNav } from "./desktop-nav";
import { PincodeChip } from "./header-actions";
import { SearchBox } from "./search-box";

function SearchFallback() {
  return (
    <div className="bg-surface-paper border-border h-11 rounded-2xl border" aria-hidden="true" />
  );
}

/**
 * Top header (D88). Phones: logo, pincode and search; navigation is the bottom tab bar. From md up the tab bar's
 * items move into this header.
 */
export function SiteHeader() {
  return (
    <header className="border-border bg-canvas/95 sticky top-0 z-[1100] border-b backdrop-blur">
      <div className="mx-auto flex max-w-7xl items-center gap-2 px-4 py-2 sm:gap-4 lg:px-8">
        <Link href="/" className="shrink-0 rounded-md" aria-label="Fresh Orbit home">
          <Logo />
        </Link>
        <PincodeChip />
        <div className="hidden min-w-0 flex-1 lg:block">
          <Suspense fallback={<SearchFallback />}>
            <SearchBox />
          </Suspense>
        </div>
        <div className="ml-auto">
          <DesktopNav />
        </div>
      </div>
      <div className="px-4 pb-2 lg:hidden">
        <Suspense fallback={<SearchFallback />}>
          <SearchBox />
        </Suspense>
      </div>
    </header>
  );
}
