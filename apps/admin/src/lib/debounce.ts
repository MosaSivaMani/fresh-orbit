import { useEffect, useState } from "react";

/** Wait after the last keystroke before a search goes to the server. */
export const SEARCH_DEBOUNCE_MS = 300;

/** `value`, once it has stopped changing for `delayMs` (search boxes that query the server). */
export function useDebouncedValue<T>(value: T, delayMs = SEARCH_DEBOUNCE_MS): T {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return settled;
}
