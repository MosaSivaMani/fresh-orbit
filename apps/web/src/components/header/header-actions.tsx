"use client";

import { Button } from "@fresh/ui";
import { ChevronDown, MapPin } from "lucide-react";
import { useLocation } from "@/location/location-context";

/** "Delivering to 530017 ▾": opens the pincode picker. Cart and account links live in the tab bar / desktop nav. */
export function PincodeChip() {
  const { pincode, ready, setPickerOpen } = useLocation();
  return (
    <Button
      variant="ghost"
      size="sm"
      className="text-text h-auto max-w-[11rem] flex-col items-start gap-0 rounded-2xl px-2 py-1 text-left"
      onClick={() => setPickerOpen(true)}
      aria-label={pincode ? `Delivering to ${pincode}. Change pincode` : "Set delivery pincode"}
    >
      <span className="text-text-muted text-[0.6875rem] font-bold tracking-[0.08em] uppercase">
        {pincode ? "Delivering to" : "Deliver to"}
      </span>
      <span className="flex items-center gap-1 text-sm font-bold">
        <MapPin aria-hidden="true" className="text-accent-text size-3.5" />
        {ready ? (pincode ?? "Set pincode") : "…"}
        <ChevronDown aria-hidden="true" className="size-3.5" />
      </span>
    </Button>
  );
}
