import { type VariantProps, cva } from "class-variance-authority";
import { Loader2 } from "lucide-react";
import { Slot } from "radix-ui";
import { type ButtonHTMLAttributes, type ReactNode, type Ref } from "react";
import { cn, focusRing } from "../lib/cn";

export const buttonVariants = cva(
  [
    "inline-flex shrink-0 cursor-pointer items-center justify-center gap-2 rounded-full font-semibold whitespace-nowrap select-none",
    "transition-[color,background-color,box-shadow,transform] duration-150 motion-safe:active:translate-y-px",
    "disabled:pointer-events-none disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50",
    "[&_svg]:pointer-events-none [&_svg]:shrink-0",
    focusRing,
  ],
  {
    variants: {
      variant: {
        primary: "bg-primary text-on-primary hover:bg-primary-hover shadow-soft-sm",
        accent: "bg-accent text-on-accent hover:bg-accent-hover shadow-soft-sm",
        highlight: "bg-highlight text-on-highlight hover:bg-highlight-hover shadow-soft-sm",
        secondary: "bg-muted text-text hover:bg-border",
        outline: "border-border-strong bg-surface text-text hover:bg-muted border",
        ghost: "text-text hover:bg-muted",
        danger: "bg-danger text-on-danger shadow-soft-sm hover:brightness-95",
        link: "text-accent-text h-auto px-0 underline-offset-4 hover:underline",
        /** Illustrated look (D53): ink outline and offset shadow, for one playful call to action. */
        sticker:
          "border-outline-ink bg-accent-coral text-on-accent-coral shadow-sticker hover:shadow-sticker-sm border-2 motion-safe:hover:translate-x-0.5 motion-safe:hover:translate-y-0.5",
      },
      size: {
        sm: "h-8 px-3 text-sm [&_svg]:size-4",
        md: "h-10 px-4 text-sm [&_svg]:size-4",
        lg: "h-12 px-6 text-base [&_svg]:size-5",
        icon: "size-10 p-0 [&_svg]:size-5",
        "icon-sm": "size-8 p-0 [&_svg]:size-4",
      },
      fullWidth: { true: "w-full" },
    },
    defaultVariants: { variant: "primary", size: "md" },
  },
);

export interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  /** Render the single child element (e.g. a link) with button styles. */
  asChild?: boolean;
  /** Shows a spinner, disables the button and sets aria-busy. */
  loading?: boolean;
  /** Icon before the label. */
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  ref?: Ref<HTMLButtonElement>;
}

export function Button({
  className,
  variant,
  size,
  fullWidth,
  asChild = false,
  loading = false,
  leadingIcon,
  trailingIcon,
  disabled,
  children,
  type,
  ref,
  ...props
}: ButtonProps) {
  const classes = cn(buttonVariants({ variant, size, fullWidth }), className);
  if (asChild) {
    return (
      <Slot.Root ref={ref} className={classes} {...props}>
        {children}
      </Slot.Root>
    );
  }
  return (
    <button
      ref={ref}
      type={type ?? "button"}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...props}
    >
      {loading ? <Loader2 aria-hidden="true" className="animate-spin" /> : leadingIcon}
      {children}
      {!loading && trailingIcon}
    </button>
  );
}

export interface IconButtonProps extends Omit<
  ButtonProps,
  "children" | "leadingIcon" | "trailingIcon" | "asChild"
> {
  /** Accessible name; required because the button shows only an icon. */
  label: string;
  icon: ReactNode;
}

/** Icon-only button. `label` becomes aria-label and a native tooltip. */
export function IconButton({
  label,
  icon,
  variant = "ghost",
  size = "icon",
  ...props
}: IconButtonProps) {
  return (
    <Button aria-label={label} title={label} variant={variant} size={size} {...props}>
      {icon}
    </Button>
  );
}
