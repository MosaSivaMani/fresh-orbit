import { Loader2 } from "lucide-react";
import { cn } from "../lib/cn";

export interface SpinnerProps {
  /** Announced to screen readers. */
  label?: string;
  size?: "sm" | "md" | "lg";
  className?: string;
}

const sizes = { sm: "size-4", md: "size-5", lg: "size-8" } as const;

/** Indeterminate progress. Has role="status" so the label is announced. */
export function Spinner({ label = "Loading", size = "md", className }: SpinnerProps) {
  return (
    <span role="status" className={cn("text-accent inline-flex items-center", className)}>
      <Loader2 aria-hidden="true" className={cn("animate-spin", sizes[size])} />
      <span className="sr-only">{label}</span>
    </span>
  );
}
