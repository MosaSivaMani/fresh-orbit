import Image from "next/image";

import { Script } from "@fresh/ui";

export type LogoTone = "default" | "coral" | "inverse";

const word: Record<LogoTone, string> = {
  default: "text-text",
  coral: "text-on-block-coral",
  inverse: "text-on-outline-ink",
};

/**
 * Wordmark: the owner's fish, crab and hen mark (D119, `docs/design/brand/`) in a white circle with an ink ring, then
 * "Fresh Orbit" with "Orbit" handwritten (D89). `coral` sits on the coral header, `inverse` on the dark footer. Decorative; the link around
 * it carries the name.
 */
export function Logo({ tone = "default" }: { tone?: LogoTone }) {
  return (
    <span className="flex items-center gap-2 whitespace-nowrap">
      <span
        aria-hidden="true"
        className="border-outline-ink size-9 shrink-0 overflow-hidden rounded-full border-[3px] bg-white"
      >
        <Image
          src="/brand/mark.png"
          alt=""
          width={64}
          height={64}
          className="size-full object-cover"
          priority
        />
      </span>
      <span className={`font-display text-[1.4375rem] leading-none font-extrabold ${word[tone]}`}>
        Fresh{" "}
        <Script tone={tone === "default" ? "accent" : "inherit"} className="text-[1.1em]">
          Orbit
        </Script>
      </span>
    </span>
  );
}
