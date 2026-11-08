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

/** Caption under every net weight we show (D348): the weight that reaches the customer, never the gross weight. */
export const NET_WEIGHT_CAPTION = "Net weight is the weight after cleaning.";

/** "5–7 pieces", "6 pieces" or "1 piece" from the server's range; null when it states none. */
export function piecesText(
  piecesMin: number | null | undefined,
  piecesMax: number | null | undefined,
): string | null {
  if (piecesMin == null || piecesMax == null) return null;
  if (piecesMin === piecesMax) return `${piecesMin} ${piecesMin === 1 ? "piece" : "pieces"}`;
  return `${piecesMin}–${piecesMax} pieces`;
}

/**
 * "500 g net · 5–7 pieces" for one fixed net-weight pack (D348): the server's net grams per pack and pieces per
 * pack, as sent. "1 kg net" for 1000 g; the pieces are left out when the server does not know them. Null without
 * the net weight, so a pack is never described by a guess.
 */
export function packFacts(
  netGrams: number | null | undefined,
  piecesMin?: number | null,
  piecesMax?: number | null,
): string | null {
  if (netGrams == null) return null;
  const pieces = piecesText(piecesMin, piecesMax);
  return pieces ? `${formatWeight(netGrams)} net · ${pieces}` : `${formatWeight(netGrams)} net`;
}

/** Pack facts of a storefront variant or list item variant sold as a PACK; null for KG and PIECE. */
export function variantPackFacts(variant: {
  sellUnit: string;
  packSizeGrams?: number | null;
  serving?: ServingInfo | null;
}): string | null {
  if (variant.sellUnit !== "PACK") return null;
  return packFacts(
    variant.packSizeGrams ?? variant.serving?.netWeightGrams,
    variant.serving?.piecesMin,
    variant.serving?.piecesMax,
  );
}

/** "12.5% off" from the server's `discountBp` (CAT-R22); undefined without a saving. */
export function discountLabel(discountBp: number | null | undefined): string | undefined {
  return discountBp ? `${formatPercentFromBp(discountBp)} off` : undefined;
}

/** "Caught 23 Oct 2026" from the batch's catch date (CAT-R25); null when the server does not know it. */
export function freshnessLine(freshness: FreshnessInfo | null | undefined): string | null {
  return freshness?.caughtOn ? `Caught ${formatCalendarDate(freshness.caughtOn)}` : null;
}
