import {
  Combobox,
  type ComboboxOption,
  FormField,
  Input,
  type InputProps,
  Select,
  type SelectOption,
  Switch,
  Textarea,
} from "@fresh/ui";
import { type ComponentProps, type ReactNode } from "react";
import {
  Controller,
  type Control,
  type FieldPath,
  type FieldValues,
  type UseFormReturn,
  get,
} from "react-hook-form";

// Thin bindings between react-hook-form and @fresh/ui fields. Field errors — from zod or from the server's
// Problem Details (applied with reportError/applyFieldErrors) — show under the field.

interface BaseFieldProps<T extends FieldValues> {
  form: UseFormReturn<T>;
  name: FieldPath<T>;
  label: ReactNode;
  hint?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  className?: string;
}

function errorOf<T extends FieldValues>(
  form: UseFormReturn<T>,
  name: FieldPath<T>,
): string | undefined {
  const err = get(form.formState.errors, name) as { message?: string } | undefined;
  return err?.message;
}

export function TextField<T extends FieldValues>({
  form,
  name,
  label,
  hint,
  required,
  disabled,
  className,
  ...input
}: BaseFieldProps<T> & Omit<InputProps, "name" | "form">) {
  return (
    <FormField
      label={label}
      hint={hint}
      error={errorOf(form, name)}
      required={required ?? false}
      disabled={disabled ?? false}
      {...(className ? { className } : {})}
    >
      <Input {...input} {...form.register(name)} />
    </FormField>
  );
}

export function TextAreaField<T extends FieldValues>({
  form,
  name,
  label,
  hint,
  required,
  disabled,
  className,
  ...rest
}: BaseFieldProps<T> & Omit<ComponentProps<"textarea">, "name" | "form">) {
  return (
    <FormField
      label={label}
      hint={hint}
      error={errorOf(form, name)}
      required={required ?? false}
      disabled={disabled ?? false}
      {...(className ? { className } : {})}
    >
      <Textarea {...rest} {...form.register(name)} />
    </FormField>
  );
}

export function SelectField<T extends FieldValues>({
  form,
  name,
  label,
  hint,
  required,
  disabled,
  className,
  options,
  placeholder,
}: BaseFieldProps<T> & { options: readonly SelectOption[]; placeholder?: string }) {
  return (
    <FormField
      label={label}
      hint={hint}
      error={errorOf(form, name)}
      required={required ?? false}
      disabled={disabled ?? false}
      {...(className ? { className } : {})}
    >
      <Controller
        control={form.control as Control<T>}
        name={name}
        render={({ field }) => (
          <Select
            options={options}
            value={(field.value as string | null | undefined) ?? ""}
            onValueChange={field.onChange}
            {...(placeholder ? { placeholder } : {})}
          />
        )}
      />
    </FormField>
  );
}

export function ComboboxField<T extends FieldValues>({
  form,
  name,
  label,
  hint,
  required,
  disabled,
  className,
  options,
  placeholder,
  searchPlaceholder,
}: BaseFieldProps<T> & {
  options: readonly ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
}) {
  return (
    <FormField
      label={label}
      hint={hint}
      error={errorOf(form, name)}
      required={required ?? false}
      disabled={disabled ?? false}
      {...(className ? { className } : {})}
    >
      <Controller
        control={form.control as Control<T>}
        name={name}
        render={({ field }) => (
          <Combobox
            options={options}
            value={(field.value as string | null | undefined) ?? null}
            onValueChange={(v) => field.onChange(v ?? "")}
            {...(placeholder ? { placeholder } : {})}
            {...(searchPlaceholder ? { searchPlaceholder } : {})}
          />
        )}
      />
    </FormField>
  );
}

export function SwitchField<T extends FieldValues>({
  form,
  name,
  label,
  description,
  disabled,
}: {
  form: UseFormReturn<T>;
  name: FieldPath<T>;
  label: string;
  description?: string;
  disabled?: boolean;
}) {
  return (
    <Controller
      control={form.control as Control<T>}
      name={name}
      render={({ field }) => (
        <Switch
          label={label}
          {...(description ? { description } : {})}
          checked={Boolean(field.value)}
          onCheckedChange={field.onChange}
          disabled={disabled ?? false}
        />
      )}
    />
  );
}
