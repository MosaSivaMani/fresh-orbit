import { Badge } from "@fresh/ui";
import Image from "next/image";
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
          <div className="border-highlight/40 bg-highlight-subtle flex h-full gap-4 overflow-hidden rounded-xl border p-5">
            <div className="flex flex-1 flex-col gap-2">
              <Badge tone="highlight" className="w-fit">
                Offer
              </Badge>
              <p className="text-text font-display text-lg font-semibold">{banner.title}</p>
              {banner.description && (
                <p className="text-text-muted text-sm">{banner.description}</p>
              )}
              {banner.linkType === "COUPON" && banner.linkTarget && (
                <p className="text-text text-sm">
                  Use code <span className="font-mono font-semibold">{banner.linkTarget}</span> in
                  your cart.
                </p>
              )}
            </div>
            {banner.imageUrl && (
              <div className="relative hidden w-28 shrink-0 overflow-hidden rounded-lg sm:block">
                <Image src={banner.imageUrl} alt="" fill sizes="112px" className="object-cover" />
              </div>
            )}
          </div>
        );
        return (
          <li key={banner.id}>
            {href ? (
              <Link href={href} className="block h-full rounded-xl">
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
