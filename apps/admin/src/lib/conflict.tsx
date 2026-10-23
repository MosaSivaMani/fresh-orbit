import { Button, DialogContent, Dialog } from "@fresh/ui";
import { useSyncExternalStore } from "react";

// One app-wide "This changed since you opened it" prompt for 412 CONCURRENCY_CONFLICT (docs/design §6.6).

interface ConflictPrompt {
  onReload: () => unknown;
}

let current: ConflictPrompt | null = null;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

export function showConflict(prompt: ConflictPrompt): void {
  current = prompt;
  emit();
}

function close() {
  current = null;
  emit();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function ConflictDialogHost() {
  const prompt = useSyncExternalStore(
    subscribe,
    () => current,
    () => current,
  );
  return (
    <Dialog open={prompt !== null} onOpenChange={(open) => !open && close()}>
      <DialogContent
        size="sm"
        title="This changed since you opened it"
        description="Someone else saved this record. Reload to see the latest version, then make your change again."
        footer={
          <>
            <Button variant="outline" onClick={close}>
              Keep editing
            </Button>
            <Button
              onClick={async () => {
                const reload = prompt?.onReload;
                close();
                await reload?.();
              }}
            >
              Reload
            </Button>
          </>
        }
      />
    </Dialog>
  );
}
