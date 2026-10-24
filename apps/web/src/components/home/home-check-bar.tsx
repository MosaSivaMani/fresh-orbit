import { Suspense } from "react";
import { SearchBox } from "@/components/header/search-box";
import { PincodeForm } from "@/location/pincode-form";

/**
 * The white bar overlapping the bottom of the home hero (D188): the pincode check (the server decides whether we
 * deliver there) and, after a divider, the product search. Phones stack the two.
 */
export function HomeCheckBar() {
  return (
    <div className="relative z-10 mx-auto -mt-10 max-w-5xl px-4 lg:-mt-14 lg:px-8">
      <div className="bg-surface grid gap-3 rounded-3xl p-3 shadow-[0_12px_32px_rgb(10_38_48/0.14)] md:grid-cols-[minmax(0,1fr)_1px_minmax(0,1.2fr)] md:items-start md:gap-4 md:p-4">
        <div className="[&>div>form]:border-border [&>div>form]:border-[1.5px] [&>div>form]:shadow-none">
          <PincodeForm variant="hero" />
        </div>
        <span aria-hidden="true" className="bg-border hidden self-stretch md:block" />
        <Suspense
          fallback={<div className="bg-surface-paper h-[58px] rounded-2xl" aria-hidden="true" />}
        >
          <SearchBox
            label="Search products"
            className="border-border rounded-2xl border-[1.5px] p-1.5"
            inputClassName="h-11 border-0 bg-transparent"
          />
        </Suspense>
      </div>
    </div>
  );
}
