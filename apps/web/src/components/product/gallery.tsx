"use client";

import { ProductGallery } from "@fresh/ui";
import { type ProductImage } from "@/api/types/catalog";

/**
 * All photos of the product in the shared 3:2 gallery (D207, D216). The gallery draws each photo with the server's
 * renditions as `srcset` (320 / 640 / 1280 px WebP), so a phone or a 64 px thumbnail downloads the small one instead
 * of the 1280 px `url` (W-9, D318). Next.js `Image` is not used here: with `images.unoptimized` it drops `srcset`.
 */
export function Gallery({ images, name }: { images: ProductImage[]; name: string }) {
  return <ProductGallery images={images} name={name} />;
}
