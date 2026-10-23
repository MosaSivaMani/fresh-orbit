import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import {
  type AdminCategory,
  type AdminProduct,
  type AdminProductListItem,
  type CreateCategoryRequest,
  type PreparationMethod,
  type ProductRequest,
  type UpdateCategoryRequest,
  type VariantRequest,
} from "./types/catalog";
import { type Id, type ListQuery, type Version } from "./types/common";

// docs/api/README.md §4.11 — categories, products, variants, preparations (yield), images.

export const catalogKeys = {
  all: ["catalog"] as const,
  categories: (includeInactive: boolean) => ["catalog", "categories", includeInactive] as const,
  products: (q: object) => ["catalog", "products", q] as const,
  product: (id: Id) => ["catalog", "product", id] as const,
  preparationMethods: ["catalog", "preparation-methods"] as const,
};

export interface ProductListQuery extends ListQuery {
  categoryId?: Id;
  status?: string | string[];
}

export function useCategories(includeInactive = true, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: catalogKeys.categories(includeInactive),
    queryFn: ({ signal }) =>
      http.get<AdminCategory[]>("/admin/categories", { query: { includeInactive }, signal }),
    staleTime: 5 * 60_000,
    enabled,
  });
}

export function useCreateCategory() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateCategoryRequest) =>
      http.post<AdminCategory>("/admin/categories", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["catalog", "categories"] }),
  });
}

export function useUpdateCategory() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      body,
      version,
    }: {
      id: Id;
      body: UpdateCategoryRequest;
      version: Version;
    }) => http.put<AdminCategory>(`/admin/categories/${id}`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["catalog", "categories"] }),
  });
}

export function useProducts(query: ProductListQuery, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: catalogKeys.products(query),
    queryFn: ({ signal }) =>
      http.get<Page<AdminProductListItem>>("/admin/products", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useProduct(id: Id, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: catalogKeys.product(id),
    queryFn: ({ signal }) => http.get<AdminProduct>(`/admin/products/${id}`, { signal }),
    enabled,
  });
}

export function usePreparationMethods(enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: catalogKeys.preparationMethods,
    queryFn: ({ signal }) =>
      http.get<PreparationMethod[]>("/admin/preparation-methods", { signal }),
    staleTime: 30 * 60_000,
    enabled,
  });
}

export function useCreateProduct() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ProductRequest) => http.post<AdminProduct>("/admin/products", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["catalog", "products"] }),
  });
}

/**
 * Every product write returns the full product with its new `version`; the editor keeps that version for
 * the next `If-Match` (product, variants and images all quote the product's version).
 */
function useProductWrite<TVars>(
  productId: Id,
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<AdminProduct>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: (product) => {
      qc.setQueryData(catalogKeys.product(productId), product);
      void qc.invalidateQueries({ queryKey: ["catalog", "products"] });
    },
  });
}

export function useUpdateProduct(productId: Id) {
  return useProductWrite(
    productId,
    (http, { body, version }: { body: ProductRequest; version: Version }) =>
      http.put<AdminProduct>(`/admin/products/${productId}`, body, { ifMatch: version }),
  );
}

export type ProductTransition = "publish" | "archive" | "restore";

export function useProductTransition(productId: Id) {
  return useProductWrite(
    productId,
    (http, { action, version }: { action: ProductTransition; version: Version }) =>
      http.post<AdminProduct>(`/admin/products/${productId}/${action}`, undefined, {
        ifMatch: version,
      }),
  );
}

export function useAddVariant(productId: Id) {
  return useProductWrite(
    productId,
    (http, { body, version }: { body: VariantRequest; version: Version }) =>
      http.post<AdminProduct>(`/admin/products/${productId}/variants`, body, { ifMatch: version }),
  );
}

export function useUpdateVariant(productId: Id) {
  return useProductWrite(
    productId,
    (
      http,
      { variantId, body, version }: { variantId: Id; body: VariantRequest; version: Version },
    ) =>
      http.put<AdminProduct>(`/admin/products/${productId}/variants/${variantId}`, body, {
        ifMatch: version,
      }),
  );
}

/** Multipart upload (`file`, `altText`); the server checks type by content, size ≤ 5 MB, shortest side ≥ 800 px. */
export function useUploadImage(productId: Id) {
  return useProductWrite(
    productId,
    async (http, { file, altText }: { file: File; altText: string }) => {
      const form = new FormData();
      form.append("file", file);
      form.append("altText", altText);
      // The backend appends images without If-Match (docs/api lists IM; implementation does not require it).
      await http.post(`/admin/products/${productId}/images`, form);
      // The upload answers with the image; re-read the product for its new version and image order.
      return http.get<AdminProduct>(`/admin/products/${productId}`);
    },
  );
}

export function useReorderImages(productId: Id) {
  return useProductWrite(
    productId,
    (http, { imageIds, version }: { imageIds: Id[]; version: Version }) =>
      http.put<AdminProduct>(
        `/admin/products/${productId}/images/order`,
        { imageIds },
        { ifMatch: version },
      ),
  );
}

export function useDeleteImage(productId: Id) {
  return useProductWrite(
    productId,
    (http, { imageId, version }: { imageId: Id; version: Version }) =>
      http.delete<AdminProduct>(`/admin/products/${productId}/images/${imageId}`, {
        ifMatch: version,
      }),
  );
}
