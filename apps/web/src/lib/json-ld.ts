// schema.org Product JSON-LD built only from server-provided values (no computed prices).
import { type ProductDetail } from "@/api/types/catalog";

const AVAILABILITY: Record<string, string> = {
  IN_STOCK: "https://schema.org/InStock",
  LOW_STOCK: "https://schema.org/LimitedAvailability",
  OUT_OF_STOCK: "https://schema.org/OutOfStock",
};

const UNIT_CODES: Record<string, string> = { KG: "KGM", PIECE: "H87", PACK: "XPK" };

/** Paise → decimal rupee string for structured data ("129900" → "1299.00"); formatting only. */
function rupeeString(paise: number): string {
  const sign = paise < 0 ? "-" : "";
  const abs = Math.abs(Math.trunc(paise));
  return `${sign}${Math.floor(abs / 100)}.${String(abs % 100).padStart(2, "0")}`;
}

export function productJsonLd(product: ProductDetail, url: string | null): Record<string, unknown> {
  const offers = product.variants
    .filter(
      (v) => v.unitPricePaise !== null && v.availability && AVAILABILITY[v.availability.status],
    )
    .map((v) => ({
      "@type": "Offer",
      sku: v.sku,
      name: v.name,
      priceCurrency: "INR",
      price: rupeeString(v.unitPricePaise!),
      availability: AVAILABILITY[v.availability!.status],
      priceSpecification: {
        "@type": "UnitPriceSpecification",
        priceCurrency: "INR",
        price: rupeeString(v.unitPricePaise!),
        referenceQuantity: {
          "@type": "QuantitativeValue",
          value: 1,
          unitCode: UNIT_CODES[v.sellUnit],
        },
      },
      ...(url ? { url } : {}),
    }));
  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    ...(product.description ? { description: product.description } : {}),
    ...(product.images.length ? { image: product.images.map((i) => i.url) } : {}),
    category: product.category.name,
    ...(product.localNames.length ? { alternateName: product.localNames } : {}),
    ...(url ? { url } : {}),
    ...(offers.length ? { offers } : {}),
  };
}

/** Serialise for a <script type="application/ld+json"> without allowing "</script>" breakouts. */
export function serializeJsonLd(data: unknown): string {
  return JSON.stringify(data).replace(/</g, "\\u003c");
}
