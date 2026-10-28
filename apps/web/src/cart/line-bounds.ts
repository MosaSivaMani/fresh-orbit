import { type CartLine } from "@/api/types/cart";

export interface LineBounds {
  min: number;
  max: number;
  step: number;
}

/**
 * The variant's order bounds (min, max, step) for a cart line's stepper, exactly as the cart line carries them (gap
 * W4 closed, docs/design/web.md §6). A line without them (an unsellable variant) gets no bounds, so the stepper stays
 * disabled instead of guessing; the server still validates every change.
 */
export function lineBounds(line: CartLine): LineBounds | null {
  if (!line.minOrder || !line.maxOrder || !line.orderStep) return null;
  return { min: line.minOrder.value, max: line.maxOrder.value, step: line.orderStep.value };
}
