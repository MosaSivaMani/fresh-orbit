import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id, type Version } from "./types/common";
import {
  type AdjustStockRequest,
  type Batch,
  type ColdChainLog,
  type CountStockRequest,
  type CreateStockItemRequest,
  type DiscardBatchRequest,
  type Movement,
  type ReceiptResponse,
  type ReceiveStockRequest,
  type RecordTemperatureRequest,
  type RecordWasteRequest,
  type Reservation,
  type StockChangeResponse,
  type StockItem,
} from "./types/inventory";

// docs/api/README.md §4.14 — stock, receipts, batches, waste, adjustments, counts, movements, cold chain.

export const inventoryKeys = {
  all: ["inventory"] as const,
  stock: (q: object) => ["inventory", "stock", q] as const,
  stockItem: (id: Id) => ["inventory", "stock-item", id] as const,
  batches: (q: object) => ["inventory", "batches", q] as const,
  movements: (q: object) => ["inventory", "movements", q] as const,
  reservations: (q: object) => ["inventory", "reservations", q] as const,
  coldChain: (q: object) => ["inventory", "cold-chain", q] as const,
};

type Query = Record<string, string | number | boolean | string[] | undefined>;

function useList<T>(key: readonly unknown[], path: string, query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => http.get<Page<T>>(path, { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useStock(query: Query, enabled = true) {
  return useList<StockItem>(inventoryKeys.stock(query), "/admin/inventory/stock", query, enabled);
}

export function useStockItem(id: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: inventoryKeys.stockItem(id),
    queryFn: ({ signal }) => http.get<StockItem>(`/admin/inventory/stock/${id}`, { signal }),
  });
}

export function useBatches(query: Query, enabled = true) {
  return useList<Batch>(inventoryKeys.batches(query), "/admin/inventory/batches", query, enabled);
}

export function useMovements(query: Query, enabled = true) {
  return useList<Movement>(
    inventoryKeys.movements(query),
    "/admin/inventory/movements",
    query,
    enabled,
  );
}

export function useReservations(query: Query, enabled = true) {
  return useList<Reservation>(
    inventoryKeys.reservations(query),
    "/admin/inventory/reservations",
    query,
    enabled,
  );
}

export function useColdChain(query: Query, enabled = true) {
  return useList<ColdChainLog>(
    inventoryKeys.coldChain(query),
    "/admin/inventory/cold-chain",
    query,
    enabled,
  );
}

/** Every inventory write changes stock figures somewhere: refresh all inventory reads and the dashboard. */
function useInventoryWrite<TVars, TResult>(
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<TResult>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: async () => {
      await Promise.all([
        qc.invalidateQueries({ queryKey: inventoryKeys.all }),
        qc.invalidateQueries({ queryKey: ["reports", "dashboard"] }),
      ]);
    },
  });
}

export function useCreateStockItem() {
  return useInventoryWrite((http, body: CreateStockItemRequest) =>
    http.post<StockItem>("/admin/inventory/stock-items", body),
  );
}

export function useSetThreshold(stockItemId: Id) {
  return useInventoryWrite(
    (http, { lowStockThreshold, version }: { lowStockThreshold: number; version: Version }) =>
      http.put<StockItem>(
        `/admin/inventory/stock/${stockItemId}/threshold`,
        { lowStockThreshold },
        { ifMatch: version },
      ),
  );
}

/** Idempotency-Key required (docs/api §1.8): one key per receive action, reused on retry. */
export function useReceiveStock() {
  return useInventoryWrite(
    (http, { body, idempotencyKey }: { body: ReceiveStockRequest; idempotencyKey: string }) =>
      http.post<ReceiptResponse>("/admin/inventory/receipts", body, { idempotencyKey }),
  );
}

export function useRecordWaste() {
  return useInventoryWrite(
    (http, { body, idempotencyKey }: { body: RecordWasteRequest; idempotencyKey: string }) =>
      http.post<StockChangeResponse>("/admin/inventory/waste", body, { idempotencyKey }),
  );
}

/** If-Match = the stock item's version. */
export function useAdjustStock() {
  return useInventoryWrite(
    (http, { body, version }: { body: AdjustStockRequest; version: Version }) =>
      http.post<StockChangeResponse>("/admin/inventory/adjustments", body, { ifMatch: version }),
  );
}

export function useCountStock() {
  return useInventoryWrite(
    (http, { body, version }: { body: CountStockRequest; version: Version }) =>
      http.post<StockChangeResponse>("/admin/inventory/counts", body, { ifMatch: version }),
  );
}

/** If-Match = the owning stock item's version (`stockItemVersion` on the batch). */
export function useDiscardBatch() {
  return useInventoryWrite(
    (
      http,
      { batchId, body, version }: { batchId: Id; body: DiscardBatchRequest; version: Version },
    ) =>
      http.post<StockChangeResponse>(`/admin/inventory/batches/${batchId}/discard`, body, {
        ifMatch: version,
      }),
  );
}

export function useRecordTemperature() {
  return useInventoryWrite((http, body: RecordTemperatureRequest) =>
    http.post<ColdChainLog>("/admin/inventory/cold-chain", body),
  );
}
