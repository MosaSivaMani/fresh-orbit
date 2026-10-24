import { Sticker } from "@fresh/ui";
import Link from "next/link";
import { type HomeBanner } from "@/api/types/catalog";

function hrefOf(banner: HomeBanner): string | null {
  if (!banner.linkTarget) return null;
  switch (banner.linkType) {
    case "CATEGORY":
      return `/shop/${encodeURIComponent(banner.linkTarget)}`;
    case "PRODUCT":
      return `/products/${encodeURIComponent(banner.linkTarget)}`;
    case "COUPON":
      return "/cart";
    default:
      return null;
  }
}

/** Offers from `GET /promotions`; all text is the server's. Renders nothing when there are no active offers. */
export function OfferStrip({ banners }: { banners: HomeBanner[] }) {
  if (banners.length === 0) return null;
  return (
    <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {banners.map((banner) => {
        const href = hrefOf(banner);
        const body = (
          <div className="border-mango bg-mango-soft flex h-full gap-4 overflow-hidden rounded-3xl border-[1.5px] border-dashed p-5">
            <div className="flex flex-1 flex-col gap-2">
              <Sticker tone="coral" size="sm" className="w-fit">
                Offer
              </Sticker>
              <p className="text-text font-display text-xl leading-tight font-extrabold">{banner.title}</p>
              {banner.subtitle && <p className="text-text-muted text-sm">{banner.subtitle}</p>}
              {banner.linkType === "COUPON" && banner.linkTarget && (
                <p className="text-text text-sm">
                  Use code <span className="font-mono font-semibold">{banner.linkTarget}</span> in
                  your cart.
                </p>
              )}
            </div>
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
