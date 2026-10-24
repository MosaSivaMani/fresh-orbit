import { cn } from "@fresh/ui";
import { Suspense } from "react";
import { SearchBox } from "./search-box";

function SearchFallback() {
  return (
    <div className="bg-surface-paper border-border h-11 rounded-[14px] border" aria-hidden="true" />
  );
}

/** The app's search bar, placed in the page (home, shop, search) as on the phone. */
export function PageSearch({ className }: { className?: string }) {
  return (
    <div className={cn("w-full max-w-[640px]", className)}>
      <Suspense fallback={<SearchFallback />}>
        <SearchBox />
      </Suspense>
    </div>
  );
}
