import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo } from "react";
import { useHttp, useSession } from "../app/services";
import { type Id, type ListQuery, type Version } from "./types/common";
import {
  type AddClosureRequest,
  type AddPincodesResponse,
  type City,
  type CityDetail,
  type CityLookup,
  type Closure,
  type CreateCityRequest,
  type CreateFulfillmentLocationRequest,
  type CreateServiceAreaRequest,
  type FulfillmentLocation,
  type Pincode,
  type Readiness,
  type ServiceArea,
  type SetOperatingHoursRequest,
  type StaffLocation,
  type UpdateCityRequest,
  type UpdateFulfillmentLocationRequest,
  type UpdateServiceAreaRequest,
} from "./types/locations";

// docs/api/README.md §4.18 — cities, fulfillment locations, service areas, pincodes, hours, closures.

export const locationKeys = {
  all: ["locations"] as const,
  cities: (q: object) => ["locations", "cities", q] as const,
  cityLookup: ["locations", "city-lookup"] as const,
  myLocations: ["locations", "me"] as const,
  city: (id: Id) => ["locations", "city", id] as const,
  readiness: (id: Id) => ["locations", "city", id, "readiness"] as const,
  fulfillmentLocations: (q: object) => ["locations", "fulfillment-locations", q] as const,
  fulfillmentLocation: (id: Id) => ["locations", "fulfillment-location", id] as const,
  closures: (id: Id) => ["locations", "fulfillment-location", id, "closures"] as const,
  serviceAreas: (q: object) => ["locations", "service-areas", q] as const,
  pincodes: (id: Id) => ["locations", "service-area", id, "pincodes"] as const,
};

export interface CityListQuery extends ListQuery {
  status?: string | string[];
}

export function useCities(query: CityListQuery, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.cities(query),
    queryFn: ({ signal }) => http.get<Page<City>>("/admin/cities", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/**
 * Every city by name for pickers and labels (`GET /admin/cities/lookup`, any staff member; G2). Editing a city still
 * needs `locations.manage`.
 */
export function useCityLookup() {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.cityLookup,
    queryFn: ({ signal }) =>
      http.get<Page<CityLookup>>("/admin/cities/lookup", { query: { pageSize: 100 }, signal }),
    staleTime: 5 * 60_000,
  });
}

export function useCity(cityId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.city(cityId),
    queryFn: ({ signal }) => http.get<CityDetail>(`/admin/cities/${cityId}`, { signal }),
  });
}

export function useCityReadiness(cityId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.readiness(cityId),
    queryFn: ({ signal }) => http.get<Readiness>(`/admin/cities/${cityId}/readiness`, { signal }),
  });
}

export function useCreateCity() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateCityRequest) => http.post<City>("/admin/cities", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useUpdateCity(cityId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, version }: { body: UpdateCityRequest; version: Version }) =>
      http.put<City>(`/admin/cities/${cityId}`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export type CityTransition = "activate" | "pause" | "retire";

/** Activate / pause / retire. Activation refused → 422 `CITY_NOT_READY` with `missing` (docs/api §4.18). */
export function useCityTransition(cityId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ action, version }: { action: CityTransition; version: Version }) =>
      http.post<City>(`/admin/cities/${cityId}/${action}`, undefined, { ifMatch: version }),
    onSettled: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export interface FulfillmentLocationQuery extends ListQuery {
  cityId?: Id;
}

export function useFulfillmentLocations(query: FulfillmentLocationQuery, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.fulfillmentLocations(query),
    queryFn: ({ signal }) =>
      http.get<Page<FulfillmentLocation>>("/admin/fulfillment-locations", {
        query: { ...query },
        signal,
      }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useFulfillmentLocation(id: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.fulfillmentLocation(id),
    queryFn: ({ signal }) =>
      http.get<FulfillmentLocation>(`/admin/fulfillment-locations/${id}`, { signal }),
  });
}

export function useCreateFulfillmentLocation() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateFulfillmentLocationRequest) =>
      http.post<FulfillmentLocation>("/admin/fulfillment-locations", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useUpdateFulfillmentLocation(id: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, version }: { body: UpdateFulfillmentLocationRequest; version: Version }) =>
      http.put<FulfillmentLocation>(`/admin/fulfillment-locations/${id}`, body, {
        ifMatch: version,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useSetOperatingHours(id: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, version }: { body: SetOperatingHoursRequest; version: Version }) =>
      http.put<FulfillmentLocation>(`/admin/fulfillment-locations/${id}/operating-hours`, body, {
        ifMatch: version,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useClosures(id: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.closures(id),
    queryFn: ({ signal }) =>
      http.get<Closure[] | Page<Closure>>(`/admin/fulfillment-locations/${id}/closures`, {
        signal,
      }),
    select: (data) => (Array.isArray(data) ? data : data.items),
  });
}

export function useAddClosure(id: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: AddClosureRequest) =>
      http.post<Closure>(`/admin/fulfillment-locations/${id}/closures`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.closures(id) }),
  });
}

export function useRemoveClosure(id: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (closureId: Id) =>
      http.delete(`/admin/fulfillment-locations/${id}/closures/${closureId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.closures(id) }),
  });
}

export interface ServiceAreaQuery extends ListQuery {
  cityId?: Id;
  fulfillmentLocationId?: Id;
}

export function useServiceAreas(query: ServiceAreaQuery, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: locationKeys.serviceAreas(query),
    queryFn: ({ signal }) =>
      http.get<Page<ServiceArea>>("/admin/service-areas", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useCreateServiceArea() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateServiceAreaRequest) =>
      http.post<ServiceArea>("/admin/service-areas", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useUpdateServiceArea(id: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ body, version }: { body: UpdateServiceAreaRequest; version: Version }) =>
      http.put<ServiceArea>(`/admin/service-areas/${id}`, body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useServiceArea(id: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: ["locations", "service-area", id],
    queryFn: ({ signal }) => http.get<ServiceArea>(`/admin/service-areas/${id}`, { signal }),
  });
}

export function usePincodes(serviceAreaId: Id, includeRemoved = false) {
  const http = useHttp();
  return useQuery({
    queryKey: [...locationKeys.pincodes(serviceAreaId), includeRemoved],
    queryFn: ({ signal }) =>
      http.get<Pincode[] | Page<Pincode>>(`/admin/service-areas/${serviceAreaId}/pincodes`, {
        query: { includeRemoved },
        signal,
      }),
    select: (data) => (Array.isArray(data) ? data : data.items),
  });
}

export function useAddPincodes(serviceAreaId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ pincodes, version }: { pincodes: string[]; version: Version }) =>
      http.post<AddPincodesResponse>(
        `/admin/service-areas/${serviceAreaId}/pincodes`,
        { pincodes },
        { ifMatch: version },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

export function useRemovePincode(serviceAreaId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ pincode, version }: { pincode: string; version: Version }) =>
      http.delete(`/admin/service-areas/${serviceAreaId}/pincodes/${pincode}`, {
        ifMatch: version,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: locationKeys.all }),
  });
}

/**
 * Locations the signed-in staff member may pick in the location switcher, with names, from
 * `GET /admin/me/locations` (any staff member; every location for all-locations staff; G1).
 */
export interface StaffLocationOption {
  id: Id;
  name: string;
  code: string | null;
}

export function useStaffLocations() {
  const http = useHttp();
  const { claims } = useSession();
  const list = useQuery({
    queryKey: locationKeys.myLocations,
    queryFn: ({ signal }) =>
      http.get<Page<StaffLocation>>("/admin/me/locations", { query: { pageSize: 100 }, signal }),
    enabled: claims !== null,
    staleTime: 5 * 60_000,
  });
  // Scoped staff keep their token's ids while the names load, so the scope is never "all locations" meanwhile.
  const named = new Map((list.data?.items ?? []).map((l) => [l.id, l]));
  const ids = claims?.allLocations ? [...named.keys()] : (claims?.locationIds ?? []);
  const rows: StaffLocationOption[] = ids.map((id) => ({
    id,
    name: named.get(id)?.name ?? `Location ${id.slice(-6)}`,
    code: named.get(id)?.code ?? null,
  }));
  // The same list keeps the same identity, so screens that depend on it do not re-render when a refetch
  // returns what they already show (an open row dialog would otherwise close).
  const key = JSON.stringify(rows);
  const locations = useMemo(() => JSON.parse(key) as StaffLocationOption[], [key]);
  return { allLocations: claims?.allLocations ?? false, isLoading: list.isLoading, locations };
}
