import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type CustomerDetail,
  type CustomerListItem,
  type CustomerListQuery,
} from "./types/customers";
import { type AdminOrderSummary } from "./types/orders";

// docs/api/README.md §4.19 — customer list, detail, orders, block / unblock.

export const customerKeys = {
  all: ["customers"] as const,
  list: (q: object) => ["customers", "list", q] as const,
  detail: (id: Id) => ["customers", "detail", id] as const,
  orders: (id: Id, q: object) => ["customers", "detail", id, "orders", q] as const,
};

export function useCustomers(query: CustomerListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: customerKeys.list(query),
    queryFn: ({ signal }) =>
      http.get<Page<CustomerListItem>>("/admin/customers", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useCustomer(customerId: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: customerKeys.detail(customerId),
    queryFn: async ({ signal }) => {
      const res = await http.request<CustomerDetail>("GET", `/admin/customers/${customerId}`, {
        signal,
      });
      return { ...res.data, version: res.data.version ?? res.version ?? "" };
    },
  });
}

/** `GET /admin/customers/{id}/orders` (docs/api §4.19; needs customers.view + orders.view). */
export function useCustomerOrders(
  customerId: Id,
  query: { page?: number; pageSize?: number },
  enabled = true,
) {
  const http = useHttp();
  return useQuery({
    queryKey: customerKeys.orders(customerId, query),
    queryFn: ({ signal }) =>
      http.get<Page<AdminOrderSummary>>(`/admin/customers/${customerId}/orders`, {
        query: { ...query },
        signal,
      }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** Block (user `DISABLED`, sessions revoked) or unblock; If-Match with the customer's version. */
export function useSetCustomerBlocked(customerId: Id) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ blocked, version }: { blocked: boolean; version: Version }) =>
      http.post<CustomerDetail>(
        `/admin/customers/${customerId}/${blocked ? "block" : "unblock"}`,
        undefined,
        { ifMatch: version },
      ),
    onSuccess: (data) => {
      qc.setQueryData(customerKeys.detail(customerId), data);
      void qc.invalidateQueries({ queryKey: ["customers", "list"] });
    },
  });
}
