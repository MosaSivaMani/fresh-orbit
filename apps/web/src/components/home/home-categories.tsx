import { CategoryTile, CategoryTileRow, SectionAction, SectionHeader } from "@fresh/ui";
import { ArrowRight, Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";

export interface HomeCategory {
  slug: string;
  name: string;
  imageUrl?: string | null;
}

/**
 * "What's on your mind?" (D188, shared `SectionHeader`, `SectionAction` and `CategoryTile`): one round picture per
 * category the server features (names and pictures are data; rule 7). A category without a picture shows the fish
 * mark.
 */
export function HomeCategories({ categories }: { categories: HomeCategory[] }) {
  if (categories.length === 0) return null;
  return (
    <section aria-labelledby="home-categories-heading" className="flex flex-col gap-6">
      <SectionHeader
        id="home-categories-heading"
        title="What's on your"
        script="mind?"
        action={
          <SectionAction asChild>
            <Link href="/shop">
              View all
              <ArrowRight aria-hidden="true" />
            </Link>
          </SectionAction>
        }
      />
      <CategoryTileRow label="Categories">
        {categories.map((category) => (
          <CategoryTile
            key={category.slug}
            asChild
            label={category.name}
            picture={
              category.imageUrl ? (
                <Image src={category.imageUrl} alt="" width={120} height={120} />
              ) : (
                <Fish className="text-accent-text" />
              )
            }
          >
            <Link href={`/shop/${category.slug}`} />
          </CategoryTile>
        ))}
      </CategoryTileRow>
    </section>
  );
}
