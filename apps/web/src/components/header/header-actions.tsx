"use client";

import { cn, focusRing } from "@fresh/ui";
import { ChevronDown, MapPin } from "lucide-react";
import { useLocation } from "@/location/location-context";

/** "Delivering to 530017 ▾" on the coral header: opens the pincode picker. */
export function PincodeChip() {
  const { pincode, ready, setPickerOpen } = useLocation();
  return (
    <button
      type="button"
      className={cn(
        "hover:bg-on-block-coral/10 flex max-w-[11rem] cursor-pointer flex-col items-start rounded-xl px-2 py-1 text-left",
        focusRing,
      )}
      onClick={() => setPickerOpen(true)}
      aria-label={pincode ? `Delivering to ${pincode}. Change pincode` : "Set delivery pincode"}
    >
      <span className="text-[0.6875rem] font-bold tracking-[0.08em] uppercase">
        {pincode ? "Delivering to" : "Deliver to"}
      </span>
      <span className="flex items-center gap-1 text-sm font-bold">
        <MapPin aria-hidden="true" className="size-3.5" />
        {ready ? (pincode ?? "Set pincode") : "…"}
        <ChevronDown aria-hidden="true" className="size-3.5" />
      </span>
    </button>
  );
}
