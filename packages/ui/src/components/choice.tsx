import { Check, Minus } from "lucide-react";
import {
  Checkbox as CheckboxPrimitive,
  RadioGroup as RadioPrimitive,
  Switch as SwitchPrimitive,
} from "radix-ui";
import { type ComponentProps, type ReactNode, useId } from "react";
import { cn, focusRing } from "../lib/cn";
import { Label } from "./form-field";

interface ChoiceLabelProps {
  /** Visible label. Omit only when an aria-label is given. */
  label?: ReactNode;
  description?: ReactNode;
}

function ChoiceText({ id, label, description }: ChoiceLabelProps & { id: string }) {
  if (!label) return null;
  return (
    <div className="grid gap-0.5">
      <Label htmlFor={id} className="cursor-pointer">
        {label}
      </Label>
      {description && (
        <p id={`${id}-description`} className="text-text-muted text-xs">
          {description}
        </p>
      )}
    </div>
  );
}

export type CheckboxProps = Omit<ComponentProps<typeof CheckboxPrimitive.Root>, "children"> &
  ChoiceLabelProps;

export function Checkbox({ label, description, id, className, ...props }: CheckboxProps) {
  const autoId = useId();
  const controlId = id ?? autoId;
  return (
    <div className="flex items-start gap-2.5">
      <CheckboxPrimitive.Root
        id={controlId}
        aria-describedby={description ? `${controlId}-description` : undefined}
        className={cn(
          "peer border-border-strong bg-surface mt-0.5 grid size-4.5 shrink-0 place-content-center rounded-sm border",
          "data-[state=checked]:border-accent data-[state=checked]:bg-accent data-[state=checked]:text-on-accent",
          "data-[state=indeterminate]:border-accent data-[state=indeterminate]:bg-accent data-[state=indeterminate]:text-on-accent",
          "disabled:cursor-not-allowed disabled:opacity-50",
          focusRing,
          className,
        )}
        {...props}
      >
        <CheckboxPrimitive.Indicator>
          {props.checked === "indeterminate" ? (
            <Minus aria-hidden="true" className="size-3.5" strokeWidth={3} />
          ) : (
            <Check aria-hidden="true" className="size-3.5" strokeWidth={3} />
          )}
        </CheckboxPrimitive.Indicator>
      </CheckboxPrimitive.Root>
      <ChoiceText id={controlId} label={label} description={description} />
    </div>
  );
}

export interface RadioOption {
  value: string;
  label: ReactNode;
  description?: ReactNode;
  disabled?: boolean;
}

export interface RadioGroupProps extends Omit<
  ComponentProps<typeof RadioPrimitive.Root>,
  "children" | "orientation"
> {
  /** Group label (rendered visibly and used as the accessible name). */
  label: ReactNode;
  options: readonly RadioOption[];
  orientation?: "vertical" | "horizontal";
  error?: ReactNode;
}

export function RadioGroup({
  label,
  options,
  orientation = "vertical",
  error,
  className,
  ...props
}: RadioGroupProps) {
  const groupId = useId();
  const labelId = `${groupId}-label`;
  const errorId = error ? `${groupId}-error` : undefined;
  return (
    <div className="flex flex-col gap-2">
      <span id={labelId} className="text-text text-sm font-medium">
        {label}
      </span>
      <RadioPrimitive.Root
        aria-labelledby={labelId}
        aria-describedby={errorId}
        orientation={orientation}
        className={cn(
          orientation === "horizontal" ? "flex flex-wrap gap-4" : "grid gap-2.5",
          className,
        )}
        {...props}
      >
        {options.map((option) => {
          const itemId = `${groupId}-${option.value}`;
          return (
            <div key={option.value} className="flex items-start gap-2.5">
              <RadioPrimitive.Item
                id={itemId}
                value={option.value}
                {...(option.disabled ? { disabled: true } : {})}
                aria-describedby={option.description ? `${itemId}-description` : undefined}
                className={cn(
                  "peer border-border-strong bg-surface mt-0.5 grid size-4.5 shrink-0 place-content-center rounded-full border",
                  "data-[state=checked]:border-accent disabled:cursor-not-allowed disabled:opacity-50",
                  focusRing,
                )}
              >
                <RadioPrimitive.Indicator className="bg-accent size-2.5 rounded-full" />
              </RadioPrimitive.Item>
              <ChoiceText id={itemId} label={option.label} description={option.description} />
            </div>
          );
        })}
      </RadioPrimitive.Root>
      {error && (
        <p id={errorId} className="text-danger-text text-xs font-medium">
          {error}
        </p>
      )}
    </div>
  );
}

export type SwitchProps = Omit<ComponentProps<typeof SwitchPrimitive.Root>, "children"> &
  ChoiceLabelProps;

export function Switch({ label, description, id, className, ...props }: SwitchProps) {
  const autoId = useId();
  const controlId = id ?? autoId;
  return (
    <div className="flex items-start gap-3">
      <SwitchPrimitive.Root
        id={controlId}
        aria-describedby={description ? `${controlId}-description` : undefined}
        className={cn(
          "peer border-border-strong bg-muted inline-flex h-6 w-10 shrink-0 cursor-pointer items-center rounded-full border p-0.5 transition-colors duration-150",
          "data-[state=checked]:border-accent data-[state=checked]:bg-accent disabled:cursor-not-allowed disabled:opacity-50",
          focusRing,
          className,
        )}
        {...props}
      >
        <SwitchPrimitive.Thumb className="bg-surface pointer-events-none block size-4.5 rounded-full shadow-sm transition-transform duration-150 data-[state=checked]:translate-x-4" />
      </SwitchPrimitive.Root>
      <ChoiceText id={controlId} label={label} description={description} />
    </div>
  );
}
