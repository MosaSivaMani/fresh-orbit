// The app's highlight marks for every inner page of the website (D181, D188): sparks beside a highlighted word, a
// swoosh under a handwritten line, a few gulls and the waves that curve a page band into the page. Drawn for the web
// in our own shapes. Every mark is decorative: aria-hidden, no text, no focus, no motion.
import { cx } from "@/lib/cx";

type MarkProps = { className?: string };

/** Three short fanned dashes next to a highlighted word. Colour = currentColor (coral on the page). */
export function Sparks({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 34 30"
      className={cx("inline-block shrink-0", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={3.6}
      strokeLinecap="round"
    >
      <path d="M6 22 10 10M14 24 24 8M22 26 32 20" />
    </svg>
  );
}

/** A loose brush stroke under a handwritten line. Colour = currentColor. */
export function Swoosh({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 140 9"
      preserveAspectRatio="none"
      className={cx("block", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={2.2}
      strokeLinecap="round"
    >
      <path d="M2 7q60-8 136-3" />
    </svg>
  );
}

/** Four gulls, two soft arcs each. Colour = currentColor. */
export function Gulls({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 120 50"
      className={cx("block", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M4 34q6-7 12 0q6-7 12 0" />
      <path d="M40 18q5-6 10 0q5-6 10 0" />
      <path d="M78 30q4-5 8 0q4-5 8 0" />
      <path d="M96 8q4-5 8 0q4-5 8 0" />
    </svg>
  );
}

/**
 * The waves at the foot of a page band: a soft teal band, a pale band, then the page colour curving up into the band.
 * Stretches to the full width.
 */
export function BandWaves({ className }: MarkProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 80"
      preserveAspectRatio="none"
      className={cx(
        "pointer-events-none absolute inset-x-0 bottom-0 block h-10 w-full sm:h-14",
        className,
      )}
    >
      <path
        d="M0 30c140-18 300-22 460-8s300 22 460 6 320-30 460-22 160 12 220 8v66H0z"
        fill="#9ED3CC"
        opacity={0.6}
      />
      <path
        d="M0 46c160-14 320-16 480-4s320 18 480 6 300-24 440-18 160 10 200 8v42H0z"
        fill="#D8EEEA"
      />
      <path
        className="fill-surface"
        d="M0 62c180-12 340-14 500-4s320 14 480 4 300-18 440-12 140 8 180 6v24H0z"
      />
    </svg>
  );
}
