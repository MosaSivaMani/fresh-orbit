import { type BubbleTone, CategoryBubble } from "@fresh/ui";
import { Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { type CategoryRef } from "@/api/types/catalog";

const tones: BubbleTone[] = ["teal", "mango", "ice", "coral"];

/** Round category bubbles in a horizontally scrolling row (D88). The current category is marked. */
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
      <ul className="-mx-4 flex gap-3 overflow-x-auto px-4 pt-1 pb-2 sm:mx-0 sm:flex-wrap sm:px-0">
        {categories.map((category, i) => (
          <li key={category.slug}>
            <CategoryBubble
              asChild
              label={category.name}
              tone={tones[i % tones.length]}
              selected={category.slug === current}
              icon={
                category.imageUrl ? (
                  <Image src={category.imageUrl} alt="" width={60} height={60} />
                ) : (
                  <Fish className="text-accent-text" />
                )
              }
            >
              <Link href={`/shop/${category.slug}`} />
            </CategoryBubble>
          </li>
        ))}
      </ul>
    </nav>
  );
}
