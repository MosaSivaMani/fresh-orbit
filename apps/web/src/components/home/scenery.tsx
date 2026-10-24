// Drawn harbour scenery for the website home (D176, D188): sparks, a swoosh, gulls, the sun over soft water, an
// orange-teal trawler and the waves that curve the hero into the page. Our own shapes, written for the web after the
// app's scenery (not copied). Every piece is decorative: aria-hidden, no text, no focus.
import { cn } from "@fresh/ui";

type ArtProps = { className?: string };

/** Three short fanned dashes, the highlight mark next to a heading or a big word. Colour = currentColor. */
export function Sparks({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 40 30"
      className={cn("inline-block shrink-0", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={3.2}
      strokeLinecap="round"
    >
      <path d="M5 24 12 12M17 21 20 5M27 22 35 13" />
    </svg>
  );
}

/** A loose brush swoosh under a handwritten line. Colour = currentColor. */
export function Swoosh({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 220 28"
      preserveAspectRatio="none"
      className={cn("block", className)}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
    >
      <path d="M6 22C60 10 130 6 214 4" strokeWidth={4.5} />
      <path d="M40 24C90 16 140 13 190 12" strokeWidth={2} opacity={0.55} />
    </svg>
  );
}

/** A gull: two soft arcs. */
function Gull({ x, y, s = 1 }: { x: number; y: number; s?: number }) {
  return (
    <path
      d={`M${x} ${y}q${7 * s} ${-7 * s} ${14 * s} ${-1 * s}q${7 * s} ${-6 * s} ${14 * s} ${1 * s}`}
    />
  );
}

/** A small flock of gulls. Colour = currentColor. */
export function Birds({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 200 90"
      className={cn("block", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={2.6}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <Gull x={8} y={60} s={1.2} />
      <Gull x={62} y={30} />
      <Gull x={118} y={52} s={0.8} />
      <Gull x={160} y={18} s={1.1} />
    </svg>
  );
}

/** An orange-teal fishing trawler riding a wave: hull, cabin with windows, wheelhouse, mast, flag and rigging. */
export function Trawler({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 240 200"
      className={cn("block", className)}
    >
      {/* rigging and mast */}
      <g stroke="#14302F" strokeWidth={2.4} strokeLinecap="round" fill="none">
        <path d="M120 18V118" strokeWidth={4} />
        <path d="M120 26 46 112M120 26 206 110M120 52 78 112" />
        <path d="M120 64H170" strokeWidth={3} />
      </g>
      <path d="M122 18h26l-6 8 6 8h-26z" fill="#F07A5A" />
      {/* cabin and wheelhouse */}
      <rect x="84" y="88" width="78" height="38" rx="5" fill="#FFFDF8" stroke="#14302F" strokeWidth={3} />
      <rect x="104" y="66" width="40" height="26" rx="4" fill="#0B7D70" stroke="#14302F" strokeWidth={3} />
      <g fill="#BFE3EE" stroke="#14302F" strokeWidth={2}>
        <rect x="92" y="98" width="13" height="11" rx="2" />
        <rect x="112" y="98" width="13" height="11" rx="2" />
        <rect x="132" y="98" width="13" height="11" rx="2" />
        <rect x="113" y="73" width="22" height="9" rx="2" />
      </g>
      {/* hull */}
      <path
        d="M24 122h196c-4 20-16 36-34 42H56c-16-6-28-22-32-42z"
        fill="#F07A5A"
        stroke="#14302F"
        strokeWidth={3.2}
        strokeLinejoin="round"
      />
      <path d="M30 136h184" stroke="#0B7D70" strokeWidth={6} />
      <circle cx="70" cy="150" r="4" fill="#FFFDF8" stroke="#14302F" strokeWidth={2} />
      <circle cx="96" cy="150" r="4" fill="#FFFDF8" stroke="#14302F" strokeWidth={2} />
      {/* the wave under it */}
      <path
        d="M0 168c20-10 40-10 60 0s40 10 60 0 40-10 60 0 40 10 60 0v32H0z"
        fill="#0B7D70"
        opacity={0.85}
      />
      <path
        d="M0 178c20-8 40-8 60 0s40 8 60 0 40-8 60 0 40 8 60 0"
        fill="none"
        stroke="#FFFDF8"
        strokeWidth={3}
        strokeLinecap="round"
      />
    </svg>
  );
}

/**
 * The sunset harbour behind the hero: the sun with a glow, a far shore, soft water bands. Sky colour comes from the
 * hero's own gradient. Scales to cover the hero (keeps the bottom edge).
 */
export function HarbourSky({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 460"
      preserveAspectRatio="xMidYMax slice"
      className={cn("absolute inset-0 size-full", className)}
    >
      <defs>
        <radialGradient id="fo-sun-glow" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#FFF1C4" stopOpacity="0.95" />
          <stop offset="0.45" stopColor="#FFD9A0" stopOpacity="0.55" />
          <stop offset="1" stopColor="#FFD9A0" stopOpacity="0" />
        </radialGradient>
        <linearGradient id="fo-water" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#CFE8E4" />
          <stop offset="1" stopColor="#A9D8D1" />
        </linearGradient>
      </defs>
      <circle cx="1290" cy="300" r="230" fill="url(#fo-sun-glow)" />
      <circle cx="1290" cy="300" r="74" fill="#FFE6A6" opacity={0.9} />
      {/* far shore and hills */}
      <path
        d="M980 330c60-34 120-46 180-40s110 12 160-14 120-60 200-38 80 30 80 30v62H980z"
        fill="#7FB7B0"
        opacity={0.45}
      />
      <path d="M0 336c80-14 160-18 260-10s160 4 220-6v40H0z" fill="#E7B48F" opacity={0.35} />
      {/* soft water */}
      <path d="M0 340c260-14 520-16 800-8s560 10 800-6v134H0z" fill="url(#fo-water)" opacity={0.75} />
      <g fill="none" stroke="#FFFDF8" strokeWidth={3} strokeLinecap="round" opacity={0.7}>
        <path d="M640 368c30-8 60-8 90 0M900 382c30-8 60-8 90 0M1120 360c24-6 48-6 72 0" />
      </g>
    </svg>
  );
}

/**
 * The waves at the bottom of the hero: a teal band and a pale band, then the page colour curving up into the hero.
 * Stretches to the full width.
 */
export function HeroWaves({ className }: ArtProps) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 1600 120"
      preserveAspectRatio="none"
      className={cn("pointer-events-none absolute inset-x-0 bottom-0 block h-[72px] w-full lg:h-[110px]", className)}
    >
      <path
        d="M0 46c120-26 240-30 380-10s260 34 420 14 300-48 460-34 220 26 340 10v94H0z"
        fill="#9ED3CC"
        opacity={0.75}
      />
      <path
        d="M0 66c140-20 280-22 420-6s300 30 460 10 300-40 440-30 200 22 280 14v66H0z"
        fill="#D8EEEA"
      />
      <path
        className="fill-surface"
        d="M0 88c160-16 320-18 480-6s320 22 480 8 320-30 460-22 140 12 180 10v42H0z"
      />
    </svg>
  );
}
