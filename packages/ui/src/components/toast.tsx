import { AlertTriangle, CheckCircle2, Info, X, XCircle } from "lucide-react";
import { Toast as ToastPrimitive } from "radix-ui";
import { type ReactNode, useSyncExternalStore } from "react";
import { cn, focusRing } from "../lib/cn";

export type ToastTone = "neutral" | "success" | "warning" | "danger" | "info";

export interface ToastInput {
  title: string;
  description?: string;
  tone?: ToastTone;
  /** Milliseconds; default 5000. Errors with an action should stay longer. */
  duration?: number;
  action?: { label: string; onClick: () => void };
}

interface ToastItem extends ToastInput {
  id: number;
}

let items: ToastItem[] = [];
let nextId = 1;
const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) listener();
}

/** Show a toast from anywhere (event handlers, mutation callbacks). Requires one <Toaster /> in the tree. */
export function toast(input: ToastInput): number {
  const id = nextId++;
  items = [...items, { ...input, id }];
  emit();
  return id;
}

export function dismissToast(id: number): void {
  items = items.filter((t) => t.id !== id);
  emit();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
const getSnapshot = () => items;

const toneStyles: Record<ToastTone, { className: string; icon: ReactNode }> = {
  neutral: { className: "border-border", icon: null },
  success: {
    className: "border-success/40",
    icon: <CheckCircle2 aria-hidden="true" className="text-success size-5" />,
  },
  warning: {
    className: "border-warning/40",
    icon: <AlertTriangle aria-hidden="true" className="text-warning size-5" />,
  },
  danger: {
    className: "border-danger/40",
    icon: <XCircle aria-hidden="true" className="text-danger size-5" />,
  },
  info: {
    className: "border-info/40",
    icon: <Info aria-hidden="true" className="text-info size-5" />,
  },
};

/** Renders queued toasts. Danger toasts are announced assertively; others politely. */
export function Toaster({ label = "Notifications" }: { label?: string }) {
  const current = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  return (
    <ToastPrimitive.Provider swipeDirection="right" label={label}>
      {current.map((t) => {
        const tone = toneStyles[t.tone ?? "neutral"];
        return (
          <ToastPrimitive.Root
            key={t.id}
            type={t.tone === "danger" ? "foreground" : "background"}
            duration={t.duration ?? 5000}
            onOpenChange={(open) => {
              if (!open) dismissToast(t.id);
            }}
            className={cn(
              "bg-surface-raised text-text animate-fo-slide-in-right pointer-events-auto relative flex w-full items-start gap-3 rounded-lg border p-4 pr-10 shadow-lg",
              tone.className,
            )}
          >
            {tone.icon}
            <div className="flex flex-1 flex-col gap-1">
              <ToastPrimitive.Title className="text-sm font-semibold">
                {t.title}
              </ToastPrimitive.Title>
              {t.description && (
                <ToastPrimitive.Description className="text-text-muted text-sm">
                  {t.description}
                </ToastPrimitive.Description>
              )}
              {t.action && (
                <ToastPrimitive.Action
                  altText={t.action.label}
                  onClick={t.action.onClick}
                  className={cn(
                    "text-accent-text mt-1 self-start rounded-md text-sm font-semibold underline-offset-4 hover:underline",
                    focusRing,
                  )}
                >
                  {t.action.label}
                </ToastPrimitive.Action>
              )}
            </div>
            <ToastPrimitive.Close
              aria-label="Dismiss notification"
              className={cn(
                "text-text-muted hover:bg-muted absolute top-3 right-3 inline-flex size-7 items-center justify-center rounded-md",
                focusRing,
              )}
            >
              <X aria-hidden="true" className="size-4" />
            </ToastPrimitive.Close>
          </ToastPrimitive.Root>
        );
      })}
      <ToastPrimitive.Viewport className="fixed right-0 bottom-0 z-[1500] m-0 flex w-full max-w-sm list-none flex-col gap-2 p-4 outline-none" />
    </ToastPrimitive.Provider>
  );
}
