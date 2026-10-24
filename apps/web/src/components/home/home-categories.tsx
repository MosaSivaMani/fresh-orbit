import { Script, focusRing } from "@fresh/ui";
import { ArrowRight, Fish } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { Sparks } from "./scenery";

export interface HomeCategory {
  slug: string;
  name: string;
  imageUrl?: string | null;
}

/**
 * "What's on your mind?" (D188): the heading with "mind?" handwritten in coral and a sparks mark, a "View all" pill,
 * and one round picture per category the server features (names and pictures are data; rule 7). A category without
 * a picture shows the fish mark. Phones scroll the row sideways inside the page gutters.
 */
export function HomeCategories({ categories }: { categories: HomeCategory[] }) {
  if (categories.length === 0) return null;
  return (
    <section aria-labelledby="home-categories-heading" className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        <h2
          id="home-categories-heading"
          className="font-display text-text flex items-start gap-1 text-[1.75rem] leading-tight font-extrabold sm:text-[2rem]"
        >
          <span>
            What&apos;s on your{" "}
            <Script className="text-accent-coral-text">mind?</Script>
          </span>
          <Sparks className="text-accent-coral mt-1 w-8" />
        </h2>
        <Link
          href="/shop"
          className={`border-primary text-primary hover:bg-primary hover:text-on-primary inline-flex shrink-0 items-center gap-2 rounded-full border-[1.5px] px-4 py-2 text-sm font-bold transition-colors ${focusRing}`}
        >
          View all
          <ArrowRight aria-hidden="true" className="size-4" />
        </Link>
      </div>
      <nav aria-label="Categories">
        <ul className="-mx-4 flex gap-4 overflow-x-auto px-4 pb-2 lg:mx-0 lg:grid lg:grid-cols-[repeat(auto-fit,minmax(120px,1fr))] lg:gap-6 lg:overflow-visible lg:px-0">
          {categories.map((category) => (
            <li key={category.slug} className="w-24 shrink-0 lg:w-auto">
              <Link
                href={`/shop/${category.slug}`}
                className={`group flex flex-col items-center gap-2 rounded-2xl text-center ${focusRing}`}
              >
                <span
                  aria-hidden="true"
                  className="bg-surface-sand grid size-24 place-items-center overflow-hidden rounded-full transition-transform group-hover:scale-[1.03] motion-reduce:transition-none lg:size-[120px] [&_img]:size-full [&_img]:object-cover [&_svg]:size-10"
                >
                  {category.imageUrl ? (
                    <Image src={category.imageUrl} alt="" width={120} height={120} />
                  ) : (
                    <Fish className="text-accent-text" />
                  )}
                </span>
                <span className="text-text text-sm leading-tight font-semibold">
                  {category.name}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </section>
  );
}
