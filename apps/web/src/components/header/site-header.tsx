import Link from "next/link";
import { Logo } from "../logo";
import { PincodeChip } from "./header-actions";
import { HeaderNav } from "./header-nav";

/**
 * The coral header (D95 food-app style): logo and pincode on the left; Offers, Help, Orders, Sign in or Account and
 * the ink cart pill on the right. The same header at every width; phones show the link icons only.
 */
export function SiteHeader() {
  return (
    <header className="bg-block-coral text-on-block-coral sticky top-0 z-[1100]">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 sm:gap-x-5 sm:px-6 lg:px-8">
        <Link href="/" className="shrink-0 rounded-md" aria-label="Fresh Orbitt home">
          <Logo tone="coral" />
        </Link>
        <PincodeChip />
        <div className="ml-auto">
          <HeaderNav />
        </div>
      </div>
    </header>
  );
}
