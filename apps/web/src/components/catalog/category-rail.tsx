import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { type CategoryNode } from "@/api/types/catalog";

/** Horizontal, keyboard-scrollable list of category links (wireframe Home §3). */
export function CategoryRail({ categories }: { categories: CategoryNode[] }) {
  if (categories.length === 0) return null;
  return (
    <ul
      className="-mx-4 flex snap-x gap-3 overflow-x-auto px-4 pb-2 sm:mx-0 sm:grid sm:grid-cols-3 sm:overflow-visible sm:px-0 lg:grid-cols-6"
      aria-label="Categories"
    >
      {categories.map((category) => (
        <li key={category.id} className="w-36 shrink-0 snap-start sm:w-auto">
          <Link
            href={`/shop/${category.slug}`}
            className="group border-border bg-surface flex h-full flex-col overflow-hidden rounded-xl border transition-shadow duration-200 hover:shadow-md"
          >
            <div className="bg-accent-subtle relative aspect-square">
              {category.imageUrl ? (
                <Image
                  src={category.imageUrl}
                  alt=""
                  fill
                  sizes="(min-width: 1024px) 16vw, 144px"
                  className="object-cover"
                />
              ) : (
                <div className="text-accent-text flex h-full items-center justify-center">
                  <Fish aria-hidden="true" className="size-10" />
                </div>
              )}
            </div>
            <span className="text-text p-3 text-sm font-semibold">{category.name}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
