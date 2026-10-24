import Link from "next/link";
import { Suspense } from "react";
import { Logo } from "../logo";
import { PincodeChip } from "./header-actions";
import { HeaderNav } from "./header-nav";
import { SearchBox } from "./search-box";

/**
 * The cream header after the owner's home reference (D188): logo, "Deliver to / Set pincode", a wide rounded search
 * field (from `md` up; phones search from the page), then Offers, Help, Orders, Sign in or Account and the dark teal
 * cart pill. Phones show the link icons only.
 */
export function SiteHeader() {
  return (
    <header className="bg-surface-paper text-text border-border sticky top-0 z-[1100] border-b">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 sm:gap-x-5 lg:gap-x-8 lg:px-8">
        <Link href="/" className="shrink-0 rounded-md" aria-label="Fresh Orbitt home">
          <Logo />
        </Link>
        <PincodeChip />
        <div className="hidden min-w-0 flex-1 md:block lg:max-w-[480px]">
          <Suspense
            fallback={<div className="bg-surface h-11 rounded-full border" aria-hidden="true" />}
          >
            <SearchBox label="Search the shop" inputClassName="bg-surface rounded-full" />
          </Suspense>
        </div>
        <div className="ml-auto">
          <HeaderNav />
        </div>
      </div>
    </header>
  );
}
