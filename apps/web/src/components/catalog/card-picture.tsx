import Image from "next/image";
import { cx } from "@/lib/cx";

/** One server rendition of a picture (`width` px wide). */
export interface PictureSource {
  width: number;
  url: string;
}

/** `srcset` from the server's renditions, smallest first (`url 320w, ...`); undefined when it sent none. */
export function pictureSrcSet(sources: readonly PictureSource[] | null | undefined) {
  const usable = (sources ?? []).filter((s) => s.url && s.width > 0);
  if (usable.length === 0) return undefined;
  return [...usable]
    .sort((a, b) => a.width - b.width)
    .map((s) => `${s.url} ${s.width}w`)
    .join(", ");
}

/**
 * A card picture filling its positioned parent. With the server's renditions it is a plain `<img>` with `srcset`, so
 * the browser downloads the smallest that fits (W-9, D375); Next.js `Image` is not used then because with
 * `images.unoptimized` it drops `srcset`. Without renditions it is the single `src`.
 */
export function CardPicture({
  src,
  sources,
  alt,
  sizes,
  priority = false,
  className,
}: {
  src: string;
  sources?: readonly PictureSource[] | null;
  alt: string;
  sizes: string;
  priority?: boolean;
  className?: string;
}) {
  const srcSet = pictureSrcSet(sources);
  if (!srcSet) {
    return (
      <Image src={src} alt={alt} fill sizes={sizes} priority={priority} className={className} />
    );
  }
  return (
    // eslint-disable-next-line @next/next/no-img-element -- next/image drops srcset when unoptimized (D375).
    <img
      src={src}
      srcSet={srcSet}
      sizes={sizes}
      alt={alt}
      loading={priority ? "eager" : "lazy"}
      fetchPriority={priority ? "high" : "auto"}
      decoding="async"
      className={cx("absolute inset-0 size-full", className)}
    />
  );
}
