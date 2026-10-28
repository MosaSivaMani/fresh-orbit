import { Sticker } from "@fresh/ui";
import Image from "next/image";
import Link from "next/link";
import { type HomeBanner } from "@/api/types/catalog";

function hrefOf(banner: HomeBanner, categorySlugs: Record<string, string>): string | null {
  if (!banner.linkTarget) return null;
  switch (banner.linkType) {
    case "CATEGORY": {
      // The server's slug for the linked category (PROMO-R28), else the featured categories' id → slug map; an
      // unknown category gets no link rather than a broken one.
      const slug = banner.linkSlug ?? categorySlugs[banner.linkTarget];
      return slug ? `/shop/${encodeURIComponent(slug)}` : null;
    }
    case "PRODUCT":
      // The banner names the product by id; only the server's slug makes a product page link.
      return banner.linkSlug ? `/products/${encodeURIComponent(banner.linkSlug)}` : null;
    case "COUPON":
      return "/cart";
    default:
      return null;
  }
}

/**
 * Offers from `GET /promotions`; all text is the server's. The banner's picture is the server's resolved `imageUrl`
 * (gap W11 closed, CAT-R28), never built from `imageKey`; the sticker is the server's `badge`, else "Offer". Renders
 * nothing when there are no active offers.
 */
export function OfferStrip({
  banners,
  categorySlugs = {},
}: {
  banners: HomeBanner[];
  /** Category id → slug, for banners that link to a category. */
  categorySlugs?: Record<string, string>;
}) {
  if (banners.length === 0) return null;
  return (
    <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {banners.map((banner) => {
        const href = hrefOf(banner, categorySlugs);
        const body = (
          <div className="border-mango bg-mango-soft flex h-full gap-4 overflow-hidden rounded-3xl border-[1.5px] border-dashed p-5">
            <div className="flex flex-1 flex-col gap-2">
              <Sticker tone="coral" size="sm" className="w-fit">
                {banner.badge ?? "Offer"}
              </Sticker>
              <p className="text-text font-display text-xl leading-tight font-extrabold">
                {banner.title}
              </p>
              {banner.subtitle && <p className="text-text-muted text-sm">{banner.subtitle}</p>}
              {banner.linkType === "COUPON" && banner.linkTarget && (
                <p className="text-text text-sm">
                  Use code <span className="font-mono font-semibold">{banner.linkTarget}</span> in
                  your cart.
                </p>
              )}
            </div>
            {banner.imageUrl && (
              <div className="relative size-24 shrink-0 self-center overflow-hidden rounded-2xl sm:size-28">
                <Image src={banner.imageUrl} alt="" fill sizes="112px" className="object-cover" />
              </div>
            )}
          </div>
        );
        return (
          <li key={banner.id}>
            {href ? (
              <Link href={href} className="block h-full rounded-3xl">
                {body}
              </Link>
            ) : (
              body
            )}
          </li>
        );
      })}
    </ul>
  );
}
