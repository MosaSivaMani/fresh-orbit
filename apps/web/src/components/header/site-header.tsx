import Link from "next/link";
import { Suspense } from "react";
import { Logo } from "../logo";
import { AccountLink, CartLink, PincodeChip } from "./header-actions";
import { SearchBox } from "./search-box";

function SearchFallback() {
  return (
    <div className="bg-surface border-border-strong h-10 rounded-md border" aria-hidden="true" />
  );
}

export function SiteHeader() {
  return (
    <header className="border-border bg-surface/95 sticky top-0 z-[1100] border-b backdrop-blur">
      <div className="mx-auto flex max-w-7xl items-center gap-2 px-4 py-2 sm:gap-4 lg:px-8">
        <Link href="/" className="shrink-0 rounded-md" aria-label="Fresh Orbit home">
          <Logo />
        </Link>
        <PincodeChip />
        <div className="hidden flex-1 md:block">
          <Suspense fallback={<SearchFallback />}>
            <SearchBox />
          </Suspense>
        </div>
        <nav aria-label="Main" className="ml-auto flex items-center gap-1">
          <Link
            href="/shop"
            className="text-text hover:bg-muted hidden h-10 items-center rounded-md px-3 text-sm font-medium sm:inline-flex"
          >
            Shop
          </Link>
          <AccountLink />
          <CartLink />
        </nav>
      </div>
      <div className="px-4 pb-2 md:hidden">
        <Suspense fallback={<SearchFallback />}>
          <SearchBox />
        </Suspense>
      </div>
    </header>
  );
}
