/** Wordmark: an orbit ring around a dot, then the name. Decorative SVG; the link around it carries the name. */
export function Logo({ inverse = false }: { inverse?: boolean }) {
  return (
    <span className="flex items-center gap-2">
      <svg viewBox="0 0 32 32" aria-hidden="true" className="size-8">
        <circle cx="16" cy="16" r="15" className={inverse ? "fill-on-primary" : "fill-primary"} />
        <ellipse
          cx="16"
          cy="16"
          rx="11"
          ry="5"
          transform="rotate(-25 16 16)"
          fill="none"
          strokeWidth="1.8"
          className="stroke-highlight"
        />
        <circle cx="16" cy="16" r="4" className={inverse ? "fill-primary" : "fill-on-primary"} />
      </svg>
      <span
        className={`font-display text-lg font-bold tracking-tight ${inverse ? "text-on-primary" : "text-primary"}`}
      >
        Fresh Orbit
      </span>
    </span>
  );
}
