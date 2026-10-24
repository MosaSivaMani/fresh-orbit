// Lines built from the server's product facts. Only formatting happens here: nothing is scaled, rounded or
// computed (rule 4), and a missing fact shows nothing rather than a guess.
import { formatCalendarDate, formatPercentFromBp, formatQuantity, formatWeight } from "@fresh/core";
import { type FreshnessInfo, type ServingInfo } from "@/api/types/catalog";

/**
 * "1 kg · 10–14 pcs · serves 4": the serving basis (its net weight when known), the pieces range and how many it
 * serves, exactly as the server states them per basis (CAT-R21). Null when it states neither pieces nor serves.
 */
export function servingLine(serving: ServingInfo | null | undefined): string | null {
  if (!serving || (serving.piecesMin === null && serving.serves === null)) return null;
  const weight = serving.netWeightGrams;
  const parts = [weight !== null ? formatWeight(weight) : formatQuantity(serving.basis)];
  const { piecesMin, piecesMax } = serving;
  if (piecesMin !== null && piecesMax !== null) {
    parts.push(piecesMin === piecesMax ? `${piecesMin} pcs` : `${piecesMin}–${piecesMax} pcs`);
  }
  if (serving.serves !== null) parts.push(`serves ${serving.serves}`);
  return parts.join(" · ");
}

/** "12.5% off" from the server's `discountBp` (CAT-R22); undefined without a saving. */
export function discountLabel(discountBp: number | null | undefined): string | undefined {
  return discountBp ? `${formatPercentFromBp(discountBp)} off` : undefined;
}

/** "Caught 23 Oct 2026" from the batch's catch date (CAT-R25); null when the server does not know it. */
export function freshnessLine(freshness: FreshnessInfo | null | undefined): string | null {
  return freshness?.caughtOn ? `Caught ${formatCalendarDate(freshness.caughtOn)}` : null;
}
