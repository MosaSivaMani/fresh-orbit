import { Script } from "@fresh/ui";

export type LogoTone = "default" | "coral" | "inverse";

const word: Record<LogoTone, string> = {
  default: "text-text",
  coral: "text-on-block-coral",
  inverse: "text-on-outline-ink",
};

/**
 * Wordmark (D95 mockup): a coral sun with an ink ring and a mango shade, then "Fresh Orbit" with "Orbit"
 * handwritten (D89). `coral` sits on the coral header, `inverse` on the dark footer. Decorative; the link around
 * it carries the name.
 */
export function Logo({ tone = "default" }: { tone?: LogoTone }) {
  return (
    <span className="flex items-center gap-2 whitespace-nowrap">
      <span
        aria-hidden="true"
        className="bg-accent-coral border-outline-ink size-7 shrink-0 rounded-full border-[3px] shadow-[inset_-6px_-4px_0_var(--fo-color-mango)]"
      />
      <span className={`font-display text-[1.4375rem] leading-none font-extrabold ${word[tone]}`}>
        Fresh{" "}
        <Script tone={tone === "default" ? "accent" : "inherit"} className="text-[1.1em]">
          Orbit
        </Script>
      </span>
    </span>
  );
}
