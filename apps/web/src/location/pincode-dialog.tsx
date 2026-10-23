"use client";

import { Dialog, DialogContent } from "@fresh/ui";
import { useLocation } from "./location-context";
import { PincodeForm } from "./pincode-form";

/** Header picker and first-visit prompt (one dialog, opened from the header chip or on first visit). */
export function PincodeDialog() {
  const { pickerOpen, setPickerOpen } = useLocation();
  return (
    <Dialog open={pickerOpen} onOpenChange={setPickerOpen}>
      <DialogContent
        title="Where should we deliver?"
        description="Enter your pincode to see prices, stock and delivery slots for your area."
      >
        <PincodeForm onDone={() => setPickerOpen(false)} />
      </DialogContent>
    </Dialog>
  );
}
