// Catalog, locations and promotions reads (docs/api §4.3, §4.4, §4.5). Typed wrappers only: no business logic.
import { type HttpClient } from "@fresh/core";
import {
  type CategoryDetail,
  type CategoryNode,
  type HomeBanner,
  type HomeResponse,
  type ProductDetail,
  type ProductListItem,
  type ProductListParams,
  type SuggestItem,
} from "../types/catalog";
import { type Page } from "../types/common";
import {
  type DeliverySlotsResponse,
  type PublicBusinessProfile,
  type PublicCity,
  type ServiceabilityResponse,
} from "../types/locations";

export function getHome(http: HttpClient): Promise<HomeResponse> {
  return http.get<HomeResponse>("/home");
}

export function getCategories(http: HttpClient): Promise<CategoryNode[]> {
  return http.get<CategoryNode[]>("/categories");
}

export function getCategory(http: HttpClient, slug: string): Promise<CategoryDetail> {
  return http.get<CategoryDetail>(`/categories/${encodeURIComponent(slug)}`);
}

export function listProducts(
  http: HttpClient,
  params: ProductListParams,
): Promise<Page<ProductListItem>> {
  return http.get<Page<ProductListItem>>("/products", {
    query: {
      category: params.category,
      q: params.q,
      sourceType: params.sourceType,
      preparation: params.preparation,
      inStock: params.inStock,
      minPricePaise: params.minPricePaise,
      maxPricePaise: params.maxPricePaise,
      sort: params.sort,
      page: params.page,
      pageSize: params.pageSize,
    },
  });
}

/** `preparation` asks the server for `maxOrderableNow` of that preparation (docs/api §4.4). */
export function getProduct(
  http: HttpClient,
  slug: string,
  preparation?: string,
): Promise<ProductDetail> {
  return http.get<ProductDetail>(`/products/${encodeURIComponent(slug)}`, {
    query: { preparation },
  });
}

export function suggest(http: HttpClient, q: string, signal?: AbortSignal): Promise<SuggestItem[]> {
  return http.get<SuggestItem[]>("/search/suggest", {
    query: { q },
    ...(signal ? { signal } : {}),
  });
}

/** Active banners for the visitor's city (`X-Pincode`); one page holds them all. */
export function getPromotions(http: HttpClient): Promise<HomeBanner[]> {
  return http.get<Page<HomeBanner>>("/promotions").then((data) => data.items);
}

/** Always 200; `serviceable: false` with a `reasonCode` when the pincode is not served. */
export function getServiceability(
  http: HttpClient,
  pincode: string,
): Promise<ServiceabilityResponse> {
  return http.get<ServiceabilityResponse>("/serviceability", { query: { pincode } });
}

export function getCities(http: HttpClient): Promise<PublicCity[]> {
  return http.get<PublicCity[]>("/cities");
}

/** The store's public contact details for the help page (gap W7). */
export function getBusinessProfile(http: HttpClient): Promise<PublicBusinessProfile> {
  return http.get<PublicBusinessProfile>("/business-profile");
}

/** Slots for a pincode (query wins over the header, docs/api §1.4). */
export function getDeliverySlots(
  http: HttpClient,
  pincode: string,
): Promise<DeliverySlotsResponse> {
  return http.get<DeliverySlotsResponse>("/delivery-slots", { query: { pincode } });
}
