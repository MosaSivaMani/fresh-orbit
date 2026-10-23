/**
 * Imagery area of the hero. Until the owner supplies photography, a calm line illustration in brand colours fills the
 * space (no gradients, no motion). Replace with a next/image photo when available.
 */
export function HeroArt() {
  return (
    <div
      className="bg-on-primary/5 border-on-primary/15 relative hidden aspect-[4/3] overflow-hidden rounded-2xl border md:block"
      aria-hidden="true"
    >
      <svg viewBox="0 0 400 300" className="absolute inset-0 size-full">
        <g fill="none" strokeLinecap="round" strokeWidth="2">
          <path
            d="M0 230 Q50 215 100 230 T200 230 T300 230 T400 230"
            className="stroke-on-primary/25"
          />
          <path
            d="M0 255 Q50 240 100 255 T200 255 T300 255 T400 255"
            className="stroke-on-primary/15"
          />
          <path
            d="M95 140 C140 85 245 85 290 140 C245 195 140 195 95 140 Z"
            className="stroke-highlight"
            strokeWidth="3"
          />
          <path
            d="M290 140 L335 105 L330 140 L335 175 Z"
            className="stroke-highlight"
            strokeWidth="3"
          />
          <path d="M150 112 C165 130 165 150 150 168" className="stroke-on-primary/50" />
          <path d="M195 103 C185 125 185 155 195 177" className="stroke-on-primary/30" />
          <path d="M235 106 C225 125 225 155 235 174" className="stroke-on-primary/30" />
        </g>
        <circle cx="128" cy="132" r="5" className="fill-highlight" />
        <g className="fill-on-primary/40">
          <circle cx="70" cy="80" r="3" />
          <circle cx="58" cy="60" r="2" />
          <circle cx="76" cy="44" r="1.5" />
        </g>
      </svg>
    </div>
  );
}
