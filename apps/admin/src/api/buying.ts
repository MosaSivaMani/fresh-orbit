import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { cleanQuery } from "./reports";
import {
  type BuyingListQuery,
  type BuyingListResponse,
  type TodaysPricesQuery,
  type TodaysPricesResponse,
} from "./types/buying";

// docs/api §4.20 — the morning buying list (REP-R14) and today's prices (REP-R15). Live queries; every number is
// computed by the server, the screens only render it.

export const buyingKeys = {
  list: (q: object) => ["buying-list", q] as const,
  prices: (q: object) => ["todays-prices", q] as const,
};

export function useBuyingList(query: BuyingListQuery | null) {
  const http = useHttp();
  return useQuery({
    queryKey: buyingKeys.list(query ?? {}),
    queryFn: ({ signal }) =>
      http.get<BuyingListResponse>("/admin/reports/buying-list", {
        query: cleanQuery(query ?? {}),
        signal,
      }),
    placeholderData: keepPreviousData,
    enabled: query !== null,
  });
}

/** Downloads the buying list as CSV through the authenticated client and saves it under the server's file name. */
export function useBuyingListCsv() {
  const http = useHttp();
  return useMutation({
    mutationFn: async (query: BuyingListQuery) => {
      const response = await http.request<string>("GET", "/admin/reports/buying-list", {
        query: cleanQuery({ ...query, format: "csv" }),
        headers: { Accept: "text/csv" },
      });
      const disposition = response.headers.get("Content-Disposition") ?? "";
      const name = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? "buying_list.csv";
      // Keep the UTF-8 byte-order mark (fetch's text() drops it) so spreadsheet apps read names right.
      const url = URL.createObjectURL(
        new Blob(["﻿", response.data ?? ""], { type: "text/csv;charset=utf-8" }),
      );
      const a = document.createElement("a");
      a.href = url;
      a.download = name;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1_000);
      return name;
    },
  });
}

export function useTodaysPrices(query: TodaysPricesQuery | null) {
  const http = useHttp();
  return useQuery({
    queryKey: buyingKeys.prices(query ?? {}),
    queryFn: ({ signal }) =>
      http.get<TodaysPricesResponse>("/admin/reports/todays-prices", {
        query: cleanQuery(query ?? {}),
        signal,
      }),
    placeholderData: keepPreviousData,
    enabled: query !== null,
  });
}
