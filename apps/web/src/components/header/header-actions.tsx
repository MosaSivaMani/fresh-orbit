"use client";

import { cn, focusRing } from "@fresh/ui";
import { ChevronDown, MapPin } from "lucide-react";
import { useLocation } from "@/location/location-context";

/** "Deliver to / Set pincode ▾" (or "Delivering to 530017") on the header: opens the pincode picker. */
export function PincodeChip() {
  const { pincode, ready, setPickerOpen } = useLocation();
  return (
    <button
      type="button"
      className={cn(
        "hover:bg-muted flex max-w-[11rem] cursor-pointer items-center gap-1.5 rounded-xl px-2 py-1 text-left",
        focusRing,
      )}
      onClick={() => setPickerOpen(true)}
      aria-label={pincode ? `Delivering to ${pincode}. Change pincode` : "Set delivery pincode"}
    >
      <MapPin aria-hidden="true" className="text-accent-coral size-5 shrink-0" />
      <span className="flex flex-col items-start">
        <span className="text-text-muted text-xs leading-tight font-semibold">
          {pincode ? "Delivering to" : "Deliver to"}
        </span>
        <span className="flex items-center gap-1 text-sm leading-tight font-bold">
          {ready ? (pincode ?? "Set pincode") : "…"}
          <ChevronDown aria-hidden="true" className="size-3.5" />
        </span>
      </span>
    </button>
  );
}
