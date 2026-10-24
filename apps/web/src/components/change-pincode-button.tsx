"use client";

import { Button, type ButtonProps } from "@fresh/ui";
import { useLocation } from "@/location/location-context";

export function ChangePincodeButton({
  label = "Set pincode",
  variant = "primary",
  size = "md",
  className,
}: {
  label?: string;
  variant?: ButtonProps["variant"];
  size?: ButtonProps["size"];
  className?: string;
}) {
  const { setPickerOpen } = useLocation();
  return (
    <Button variant={variant} size={size} className={className} onClick={() => setPickerOpen(true)}>
      {label}
    </Button>
  );
}
