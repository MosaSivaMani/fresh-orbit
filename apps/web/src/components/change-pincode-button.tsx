"use client";

import { Button, type ButtonProps } from "@fresh/ui";
import { useLocation } from "@/location/location-context";

export function ChangePincodeButton({
  label = "Set pincode",
  variant = "primary",
  size = "md",
}: {
  label?: string;
  variant?: ButtonProps["variant"];
  size?: ButtonProps["size"];
}) {
  const { setPickerOpen } = useLocation();
  return (
    <Button variant={variant} size={size} onClick={() => setPickerOpen(true)}>
      {label}
    </Button>
  );
}
