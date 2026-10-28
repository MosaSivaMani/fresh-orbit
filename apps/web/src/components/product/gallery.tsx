"use client";

import { ProductGallery, type ProductGalleryImageRenderProps } from "@fresh/ui";
import Image from "next/image";
import { type ProductImage } from "@/api/types/catalog";

function NextImage({ src, alt, variant, priority, className }: ProductGalleryImageRenderProps) {
  return (
    <Image
      src={src}
      alt={alt}
      fill
      priority={priority}
      sizes={variant === "slide" ? "(min-width: 1024px) 50vw, 100vw" : "64px"}
      className={className}
    />
  );
}

/** All photos of the product in the shared 3:2 gallery (D207, D216), drawn with Next.js `Image`. */
export function Gallery({ images, name }: { images: ProductImage[]; name: string }) {
  return <ProductGallery images={images} name={name} renderImage={NextImage} />;
}
