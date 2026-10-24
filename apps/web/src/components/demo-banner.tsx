/**
 * Shown on every page of the free public demo only (`isDemoMode()`): the shop is sample data, so nobody mistakes it
 * for the real store. Fixed text; no prices or other business values.
 */
export function DemoBanner({ enabled }: { enabled: boolean }) {
  if (!enabled) return null;
  return (
    <div
      role="note"
      aria-label="Demo store"
      className="bg-highlight-subtle text-highlight-text px-4 py-2 text-center text-sm font-semibold"
    >
      DEMO, test data only. Nothing here is real: orders are not delivered and no money is taken.
    </div>
  );
}
