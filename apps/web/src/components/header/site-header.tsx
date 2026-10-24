import Link from "next/link";
import { Logo } from "../logo";
import { PincodeChip } from "./header-actions";
import { AppTopTabs } from "./tab-bar";

/**
 * Top bar (D88, web mockup): logo and pincode on the left. From md up the app's tab bar sits on the right (icon
 * over label, teal pill and underline on the active tab, coral cart badge); on phones it stays at the bottom.
 * Search lives in the page content (home, shop, search), as in the app.
 */
export function SiteHeader() {
  return (
    <header className="border-border bg-surface-paper/95 sticky top-0 z-[1100] border-b backdrop-blur">
      <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-2 sm:gap-5 lg:px-8">
        <Link href="/" className="shrink-0 rounded-md" aria-label="Fresh Orbit home">
          <Logo />
        </Link>
        <PincodeChip />
        <div className="ml-auto">
          <AppTopTabs />
        </div>
      </div>
    </header>
  );
}
