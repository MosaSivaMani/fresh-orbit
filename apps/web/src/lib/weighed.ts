import { type CartLine } from "@/api/types/cart";

/**
 * D347: the price shown is the most you pay. Shown wherever weighed items appear and the server's maximum equals
 * its estimate (no over-weight allowance configured).
 */
export const WEIGHED_NOTE =
  "Weighed items are priced on net weight after cleaning. If your pack weighs less, you pay less, never more.";

/** True when a cart line is sold by weight (the server's sell unit), so its pack is weighed after cleaning. */
export function isWeighedLine(line: Pick<CartLine, "sellUnit">): boolean {
  return line.sellUnit === "KG";
}

/** True when any of the cart's lines is sold by weight. */
export function hasWeighedLines(lines: readonly Pick<CartLine, "sellUnit">[]): boolean {
  return lines.some(isWeighedLine);
}
