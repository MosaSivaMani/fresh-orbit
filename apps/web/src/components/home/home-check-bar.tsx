import { CheckBar } from "@fresh/ui";
import { Suspense } from "react";
import { SearchBox } from "@/components/header/search-box";
import { PincodeForm } from "@/location/pincode-form";

/**
 * The white bar overlapping the bottom of the home hero (D188, shared `CheckBar`): the pincode check (the server
 * decides whether we deliver there) and, after a divider, the product search. Phones stack the two.
 */
export function HomeCheckBar() {
  return (
    <CheckBar
      start={<PincodeForm variant="hero" />}
      end={
        <Suspense
          fallback={<div className="bg-surface-paper h-[58px] rounded-2xl" aria-hidden="true" />}
        >
          <SearchBox
            label="Search products"
            className="border-border rounded-2xl border-[1.5px] p-1.5"
            inputClassName="h-11 border-0 bg-transparent"
          />
        </Suspense>
      }
    />
  );
}
