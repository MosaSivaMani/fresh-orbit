import { type ComponentProps, type ReactNode } from "react";
import { cn, focusRing } from "../lib/cn";
import { useFormFieldControl } from "./form-field";

export const controlBase = cn(
  "border-border-strong bg-surface text-text w-full rounded-md border text-sm shadow-xs",
  "placeholder:text-text-muted disabled:cursor-not-allowed disabled:opacity-50",
  "aria-invalid:border-danger aria-invalid:focus-visible:outline-danger",
  focusRing,
);

export interface InputProps extends ComponentProps<"input"> {
  /** Decorative element inside the start of the field (icon, "₹", "+91"). */
  startAdornment?: ReactNode;
  endAdornment?: ReactNode;
}

export function Input({
  className,
  startAdornment,
  endAdornment,
  type = "text",
  ...props
}: InputProps) {
  const a11y = useFormFieldControl(props);
  const input = (
    <input
      type={type}
      className={cn(
        controlBase,
        "h-10 px-3",
        startAdornment != null && "pl-9",
        endAdornment != null && "pr-9",
        className,
      )}
      {...a11y}
    />
  );
  if (startAdornment == null && endAdornment == null) return input;
  return (
    <div className="relative">
      {startAdornment != null && (
        <span className="text-text-muted pointer-events-none absolute inset-y-0 left-3 flex items-center [&_svg]:size-4">
          {startAdornment}
        </span>
      )}
      {input}
      {endAdornment != null && (
        <span className="text-text-muted absolute inset-y-0 right-3 flex items-center [&_svg]:size-4">
          {endAdornment}
        </span>
      )}
    </div>
  );
}

export function Textarea({ className, rows = 4, ...props }: ComponentProps<"textarea">) {
  const a11y = useFormFieldControl(props);
  return (
    <textarea rows={rows} className={cn(controlBase, "min-h-20 px-3 py-2", className)} {...a11y} />
  );
}
