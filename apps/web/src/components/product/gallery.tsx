"use client";

import { Fish } from "lucide-react";
import Image from "next/image";
import { type KeyboardEvent, useState } from "react";
import { type ProductImage } from "@/api/types/catalog";
import { cx } from "@/lib/cx";

/** Main image with thumbnails; arrow keys on a thumbnail move between images. */
export function Gallery({ images, name }: { images: ProductImage[]; name: string }) {
  const [index, setIndex] = useState(0);
  const current = images[index];

  if (!current) {
    return (
      <div className="bg-muted text-text-muted flex aspect-[4/3] items-center justify-center rounded-2xl">
        <Fish aria-hidden="true" className="size-16 opacity-50" />
        <span className="sr-only">No photo of {name} yet</span>
      </div>
    );
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === "ArrowRight") setIndex((i) => (i + 1) % images.length);
    if (event.key === "ArrowLeft") setIndex((i) => (i - 1 + images.length) % images.length);
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="bg-muted relative aspect-[4/3] overflow-hidden rounded-2xl">
        <Image
          src={current.url}
          alt={current.altText || name}
          fill
          priority
          sizes="(min-width: 1024px) 50vw, 100vw"
          className="object-cover"
        />
      </div>
      {images.length > 1 && (
        <div
          role="group"
          aria-label="Product photos"
          className="flex gap-2 overflow-x-auto"
        >
          {images.map((image, i) => (
            <button
              key={image.url}
              type="button"
              onClick={() => setIndex(i)}
              onKeyDown={onKeyDown}
              aria-label={`Show photo ${i + 1} of ${images.length}`}
              aria-pressed={i === index}
              className={cx(
                "bg-muted relative size-16 shrink-0 overflow-hidden rounded-lg border-2",
                i === index ? "border-accent" : "border-transparent",
              )}
            >
              <Image src={image.url} alt="" fill sizes="64px" className="object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
