/**
 * Joins class names. Server Components cannot call `cn` from @fresh/ui (that bundle is a client module), so the
 * website uses this for its own markup; conflicting Tailwind classes are simply avoided.
 */
export function cx(...values: (string | false | null | undefined)[]): string {
  return values.filter(Boolean).join(" ");
}
