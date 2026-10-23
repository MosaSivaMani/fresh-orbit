import { AlertCircle } from "lucide-react";
import { Label as LabelPrimitive } from "radix-ui";
import { type ComponentProps, type ReactNode, createContext, useContext, useId } from "react";
import { cn } from "../lib/cn";

interface FormFieldContextValue {
  id: string;
  hintId: string | undefined;
  errorId: string | undefined;
  invalid: boolean;
  required: boolean;
  disabled: boolean;
}

const FormFieldContext = createContext<FormFieldContextValue | null>(null);

export function useFormField(): FormFieldContextValue | null {
  return useContext(FormFieldContext);
}

interface ControlA11yProps {
  id?: string | undefined;
  required?: boolean | undefined;
  disabled?: boolean | undefined;
  "aria-describedby"?: string | undefined;
  "aria-invalid"?: ComponentProps<"input">["aria-invalid"];
}

/** Merge a control's own props with the surrounding FormField (id, describedby, invalid, required). */
export function useFormFieldControl<P extends ControlA11yProps>(props: P): P {
  const field = useFormField();
  if (!field) return props;
  const describedBy = [props["aria-describedby"], field.hintId, field.errorId]
    .filter(Boolean)
    .join(" ");
  return {
    ...props,
    id: props.id ?? field.id,
    required: props.required ?? (field.required || undefined),
    disabled: props.disabled ?? (field.disabled || undefined),
    "aria-describedby": describedBy || undefined,
    "aria-invalid": props["aria-invalid"] ?? (field.invalid || undefined),
  };
}

export function Label({ className, ...props }: ComponentProps<typeof LabelPrimitive.Root>) {
  return (
    <LabelPrimitive.Root
      className={cn("text-text text-sm leading-5 font-medium peer-disabled:opacity-60", className)}
      {...props}
    />
  );
}

export interface FormFieldProps {
  label: ReactNode;
  /** Help text shown under the control and linked with aria-describedby. */
  hint?: ReactNode;
  /** Error text; marks the control aria-invalid. Show the server's `errors[field][0].message`. */
  error?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  /** Visually hide the label (it stays available to screen readers). */
  hideLabel?: boolean;
  id?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Label + control + hint + error. Controls from this package (Input, Textarea, Select, Combobox) pick up the
 * id and ARIA wiring automatically; for other controls call `useFormFieldControl`.
 */
export function FormField({
  label,
  hint,
  error,
  required = false,
  disabled = false,
  hideLabel = false,
  id,
  className,
  children,
}: FormFieldProps) {
  const autoId = useId();
  const controlId = id ?? `field-${autoId}`;
  const hintId = hint ? `${controlId}-hint` : undefined;
  const errorId = error ? `${controlId}-error` : undefined;
  const value: FormFieldContextValue = {
    id: controlId,
    hintId,
    errorId,
    invalid: Boolean(error),
    required,
    disabled,
  };

  return (
    <FormFieldContext.Provider value={value}>
      <div className={cn("flex flex-col gap-1.5", className)}>
        <Label htmlFor={controlId} className={cn(hideLabel && "sr-only")}>
          {label}
          {required && (
            <span aria-hidden="true" className="text-danger-text ml-0.5">
              *
            </span>
          )}
        </Label>
        {children}
        {hint && (
          <p id={hintId} className="text-text-muted text-xs">
            {hint}
          </p>
        )}
        {error && (
          <p id={errorId} className="text-danger-text flex items-start gap-1 text-xs font-medium">
            <AlertCircle aria-hidden="true" className="mt-px size-3.5 shrink-0" />
            <span>{error}</span>
          </p>
        )}
      </div>
    </FormFieldContext.Provider>
  );
}
