import { createIdempotencyKey } from "@fresh/core";
import { useState } from "react";

/**
 * One Idempotency-Key per user action (docs/api §1.8): the same key is sent on every retry of that action and
 * replaced only after success, or when the user changes what they are submitting.
 */
export function useIdempotencyKey(): { current: () => string; reset: () => void } {
  const [holder] = useState(createIdempotencyKey);
  return holder;
}
