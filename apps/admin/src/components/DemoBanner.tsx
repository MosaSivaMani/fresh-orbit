/** True only on the free public demo (render.yaml): `VITE_DEMO_MODE=true`, inlined at build time. */
export function isDemoMode(value: string | undefined): boolean {
  return value?.trim() === "true";
}

/**
 * Shown above every screen of the free public demo only: the store is sample data and staff accounts are shared, so
 * nobody mistakes it for the real dashboard. Fixed text; no prices or other business values.
 */
export function DemoBanner({ enabled }: { enabled: boolean }) {
  if (!enabled) return null;
  return (
    <div
      role="note"
      aria-label="Demo dashboard"
      className="bg-highlight-subtle text-highlight-text px-4 py-2 text-center text-sm font-semibold"
    >
      DEMO, test data only. Sample store and sample staff accounts; nothing here is real.
    </div>
  );
}
