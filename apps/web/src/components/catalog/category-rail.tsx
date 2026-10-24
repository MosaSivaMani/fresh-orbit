import { CategoryCircle } from "@fresh/ui";
import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { type CategoryRef } from "@/api/types/catalog";

/**
 * The round "What's on your mind?" row (D95): one circle per category from the API, scrolling sideways on phones
 * and wrapping from sm up. The current category is marked.
 */
export function CategoryRail({
  categories,
  current,
  label = "Categories",
}: {
  categories: (CategoryRef & { id?: string; imageUrl?: string | null })[];
  current?: string;
  label?: string;
}) {
  if (categories.length === 0) return null;
  return (
    <nav aria-label={label}>
      <ul className="-mx-4 flex gap-3 overflow-x-auto px-4 pt-1 pb-2 sm:mx-0 sm:flex-wrap sm:gap-4 sm:px-0">
        {categories.map((category) => (
          <li key={category.slug}>
            <CategoryCircle
              asChild
              label={category.name}
              selected={category.slug === current}
              icon={
                category.imageUrl ? (
                  <Image src={category.imageUrl} alt="" width={110} height={110} />
                ) : (
                  <Fish className="text-accent-text" />
                )
              }
            >
              <Link href={`/shop/${category.slug}`} />
            </CategoryCircle>
          </li>
        ))}
      </ul>
    </nav>
  );
}
