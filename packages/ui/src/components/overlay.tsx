import { type VariantProps, cva } from "class-variance-authority";
import { X } from "lucide-react";
import {
  Dialog as DialogPrimitive,
  Tabs as TabsPrimitive,
  Tooltip as TooltipPrimitive,
} from "radix-ui";
import { type ComponentProps, type ReactNode } from "react";
import { cn, focusRing } from "../lib/cn";

// ---------- Dialog ----------
export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogClose = DialogPrimitive.Close;

function Overlay() {
  return (
    <DialogPrimitive.Overlay className="bg-overlay/60 animate-fo-fade-in fixed inset-0 z-[1300]" />
  );
}

function CloseButton() {
  return (
    <DialogPrimitive.Close
      className={cn(
        "text-text-muted hover:bg-muted hover:text-text absolute top-3 right-3 inline-flex size-9 items-center justify-center rounded-md",
        focusRing,
      )}
      aria-label="Close"
    >
      <X aria-hidden="true" className="size-5" />
    </DialogPrimitive.Close>
  );
}

interface PanelHeaderProps {
  /** Required: names the dialog for screen readers. */
  title: ReactNode;
  description?: ReactNode;
  hideTitle?: boolean;
}

function PanelHeader({ title, description, hideTitle }: PanelHeaderProps) {
  return (
    <div className={cn("flex flex-col gap-1 pr-10", hideTitle && "sr-only")}>
      <DialogPrimitive.Title className="text-text text-lg leading-7 font-semibold">
        {title}
      </DialogPrimitive.Title>
      {description ? (
        <DialogPrimitive.Description className="text-text-muted text-sm">
          {description}
        </DialogPrimitive.Description>
      ) : null}
    </div>
  );
}

const dialogSizes = { sm: "max-w-sm", md: "max-w-lg", lg: "max-w-2xl", xl: "max-w-4xl" } as const;

export interface DialogContentProps
  extends Omit<ComponentProps<typeof DialogPrimitive.Content>, "title">, PanelHeaderProps {
  size?: keyof typeof dialogSizes;
  /** Buttons row at the bottom (primary action last). */
  footer?: ReactNode;
}

export function DialogContent({
  title,
  description,
  hideTitle,
  size = "md",
  footer,
  className,
  children,
  ...props
}: DialogContentProps) {
  return (
    <DialogPrimitive.Portal>
      <Overlay />
      <DialogPrimitive.Content
        {...(description ? {} : { "aria-describedby": undefined })}
        className={cn(
          "border-border bg-surface text-text animate-fo-pop-in fixed top-1/2 left-1/2 z-[1400] flex max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] -translate-x-1/2 -translate-y-1/2 flex-col gap-4 overflow-y-auto rounded-xl border p-6 shadow-xl",
          dialogSizes[size],
          className,
        )}
        {...props}
      >
        <PanelHeader title={title} description={description} hideTitle={hideTitle} />
        {children}
        {footer && (
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">{footer}</div>
        )}
        <CloseButton />
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

// ---------- Sheet (drawer) ----------
export const Sheet = DialogPrimitive.Root;
export const SheetTrigger = DialogPrimitive.Trigger;
export const SheetClose = DialogPrimitive.Close;

const sheetVariants = cva(
  "border-border bg-surface text-text fixed z-[1400] flex flex-col gap-4 overflow-y-auto p-6 shadow-xl",
  {
    variants: {
      side: {
        right: "animate-fo-slide-in-right inset-y-0 right-0 h-full w-full max-w-md border-l",
        left: "animate-fo-slide-in-left inset-y-0 left-0 h-full w-72 max-w-[85vw] border-r",
        bottom:
          "animate-fo-slide-in-bottom inset-x-0 bottom-0 max-h-[85dvh] rounded-t-2xl border-t",
      },
    },
    defaultVariants: { side: "right" },
  },
);

export interface SheetContentProps
  extends
    Omit<ComponentProps<typeof DialogPrimitive.Content>, "title">,
    PanelHeaderProps,
    VariantProps<typeof sheetVariants> {
  footer?: ReactNode;
}

export function SheetContent({
  title,
  description,
  hideTitle,
  side,
  footer,
  className,
  children,
  ...props
}: SheetContentProps) {
  return (
    <DialogPrimitive.Portal>
      <Overlay />
      <DialogPrimitive.Content
        {...(description ? {} : { "aria-describedby": undefined })}
        className={cn(sheetVariants({ side }), className)}
        {...props}
      >
        <PanelHeader title={title} description={description} hideTitle={hideTitle} />
        <div className="flex-1">{children}</div>
        {footer && <div className="border-border flex flex-col gap-2 border-t pt-4">{footer}</div>}
        <CloseButton />
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

// ---------- Tabs ----------
export const Tabs = TabsPrimitive.Root;

export function TabsList({ className, ...props }: ComponentProps<typeof TabsPrimitive.List>) {
  return (
    <TabsPrimitive.List
      className={cn("border-border inline-flex items-center gap-1 border-b", className)}
      {...props}
    />
  );
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof TabsPrimitive.Trigger>) {
  return (
    <TabsPrimitive.Trigger
      className={cn(
        "text-text-muted -mb-px inline-flex h-10 cursor-pointer items-center gap-2 border-b-2 border-transparent px-3 text-sm font-medium",
        "hover:text-text data-[state=active]:border-accent data-[state=active]:text-text disabled:opacity-50",
        focusRing,
        className,
      )}
      {...props}
    />
  );
}

export function TabsContent({ className, ...props }: ComponentProps<typeof TabsPrimitive.Content>) {
  return <TabsPrimitive.Content className={cn("pt-4", focusRing, className)} {...props} />;
}

// ---------- Tooltip ----------
export const TooltipProvider = TooltipPrimitive.Provider;

export interface TooltipProps {
  content: ReactNode;
  /** The trigger; must be a focusable element so keyboard users get the tooltip too. */
  children: ReactNode;
  side?: "top" | "right" | "bottom" | "left";
  delayDuration?: number;
}

/** Supplementary hint only; never put essential information in a tooltip. */
export function Tooltip({ content, children, side = "top", delayDuration = 300 }: TooltipProps) {
  return (
    <TooltipPrimitive.Provider delayDuration={delayDuration}>
      <TooltipPrimitive.Root>
        <TooltipPrimitive.Trigger asChild>{children}</TooltipPrimitive.Trigger>
        <TooltipPrimitive.Portal>
          <TooltipPrimitive.Content
            side={side}
            sideOffset={6}
            className="bg-text text-text-inverse animate-fo-fade-in z-[1600] max-w-xs rounded-md px-2.5 py-1.5 text-xs shadow-md"
          >
            {content}
            <TooltipPrimitive.Arrow className="fill-text" />
          </TooltipPrimitive.Content>
        </TooltipPrimitive.Portal>
      </TooltipPrimitive.Root>
    </TooltipPrimitive.Provider>
  );
}
