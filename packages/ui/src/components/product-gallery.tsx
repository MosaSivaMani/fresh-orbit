// The product page's picture gallery (D193, D216: pictures sit in a 3:2 frame everywhere; D207: the
// reusable part lives here, the page only composes it). Every photo sits on one horizontal scroll-snap
// track, so a touch swipe moves between them natively; dots and thumbnails jump to a photo, and Left /
// Right arrow keys move through them. Router- and framework-free: the app passes its own image component
// through `renderImage`.
import { Fish } from "lucide-react";
import { type KeyboardEvent, type ReactNode, useRef, useState } from "react";
import { cn, focusRing } from "../lib/cn";

/** One photo of the product, as the server sends it. */
export interface ProductGalleryImage {
  url: string;
  /** The server's alt text; when empty the product name is used. */
  altText?: string | null;
}

/** What `renderImage` receives for one picture. */
export interface ProductGalleryImageRenderProps {
  src: string;
  /** Alt text for a slide; empty for a thumbnail (its button carries the label). */
  alt: string;
  /** `slide` for the 3:2 frame, `thumbnail` for the 64 px buttons. */
  variant: "slide" | "thumbnail";
  /** True for the first slide only: load it eagerly at high priority; the rest load lazily. */
  priority: boolean;
  /** Classes for the picture; it fills its positioned (`relative`) parent. */
  className: string;
}

export interface ProductGalleryProps {
  images: readonly ProductGalleryImage[];
  /** The product name: labels the gallery and stands in for missing alt text. */
  name: string;
  /**
   * Draws one picture filling its positioned parent (Next.js `Image` with `fill` on the website).
   * Default a plain `<img>`.
   */
  renderImage?: (props: ProductGalleryImageRenderProps) => ReactNode;
  className?: string;
}

function DefaultImage({ src, alt, priority, className }: ProductGalleryImageRenderProps) {
  return (
    <img
      src={src}
      alt={alt}
      loading={priority ? "eager" : "lazy"}
      fetchPriority={priority ? "high" : "auto"}
      decoding="async"
      className={cn("absolute inset-0 size-full", className)}
    />
  );
}

function prefersReducedMotion(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}

/**
 * All photos of a product in a 3:2 frame: swipe (scroll-snap), dots, thumbnails from `md` up and
 * arrow keys. With no photos it shows the fish mark and says so to screen readers.
 */
export function ProductGallery({
  images,
  name,
  renderImage = DefaultImage,
  className,
}: ProductGalleryProps) {
  const [index, setIndex] = useState(0);
  const trackRef = useRef<HTMLDivElement>(null);
  const count = images.length;

  if (count === 0) {
    return (
      <div
        className={cn(
          "text-accent-text flex aspect-[3/2] items-center justify-center rounded-[22px]",
          className,
        )}
      >
        <Fish aria-hidden="true" className="size-24" strokeWidth={1.25} />
        <span className="sr-only">No photo of {name} yet</span>
      </div>
    );
  }

  const current = Math.min(index, count - 1);

  function goTo(target: number) {
    const next = (target + count) % count;
    setIndex(next);
    const track = trackRef.current;
    if (track && typeof track.scrollTo === "function") {
      track.scrollTo({
        left: next * track.clientWidth,
        behavior: prefersReducedMotion() ? "auto" : "smooth",
      });
    }
  }

  function onScroll() {
    const track = trackRef.current;
    if (!track || track.clientWidth === 0) return;
    const slide = Math.round(track.scrollLeft / track.clientWidth);
    if (slide >= 0 && slide < count && slide !== current) setIndex(slide);
  }

  function onKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (count < 2) return;
    if (event.key === "ArrowRight") {
      event.preventDefault();
      goTo(current + 1);
    } else if (event.key === "ArrowLeft") {
      event.preventDefault();
      goTo(current - 1);
    }
  }

  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- keys bubble from track and buttons
    <section
      aria-roledescription="carousel"
      aria-label={`${name} photos`}
      onKeyDown={onKeyDown}
      className={cn("flex flex-col gap-3", className)}
    >
      <div
        ref={trackRef}
        onScroll={onScroll}
        // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- a scroll area needs keyboard focus
        tabIndex={0}
        className={cn(
          "flex aspect-[3/2] snap-x snap-mandatory overflow-x-auto overscroll-x-contain rounded-[22px] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden",
          focusRing,
        )}
      >
        {images.map((image, i) => (
          <div
            key={`${i}:${image.url}`}
            role="group"
            aria-roledescription="slide"
            aria-label={`${i + 1} of ${count}`}
            className="relative w-full shrink-0 snap-center snap-always overflow-hidden"
          >
            {renderImage({
              src: image.url,
              alt: image.altText || name,
              variant: "slide",
              priority: i === 0,
              className: "object-cover",
            })}
          </div>
        ))}
      </div>
      {count > 1 && (
        <>
          <div role="group" aria-label="Choose a photo" className="flex justify-center gap-1">
            {images.map((image, i) => (
              <button
                key={`${i}:${image.url}`}
                type="button"
                onClick={() => goTo(i)}
                aria-label={`Show photo ${i + 1} of ${count}`}
                aria-current={i === current ? "true" : undefined}
                className={cn("flex size-6 items-center justify-center rounded-full", focusRing)}
              >
                <span
                  aria-hidden="true"
                  className={cn(
                    "block h-2 rounded-full transition-all motion-reduce:transition-none",
                    i === current ? "bg-accent w-5" : "bg-border-strong w-2",
                  )}
                />
              </button>
            ))}
          </div>
          <div
            role="group"
            aria-label="Photo thumbnails"
            className="hidden gap-2 overflow-x-auto md:flex"
          >
            {images.map((image, i) => (
              <button
                key={`${i}:${image.url}`}
                type="button"
                onClick={() => goTo(i)}
                aria-label={`Show photo ${i + 1} of ${count}`}
                aria-current={i === current ? "true" : undefined}
                className={cn(
                  "bg-muted relative aspect-[3/2] w-16 shrink-0 overflow-hidden rounded-lg border-2",
                  i === current ? "border-accent" : "border-transparent",
                  focusRing,
                )}
              >
                {renderImage({
                  src: image.url,
                  alt: "",
                  variant: "thumbnail",
                  priority: false,
                  className: "object-cover",
                })}
              </button>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
