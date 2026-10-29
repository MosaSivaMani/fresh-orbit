import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { WORK_LIST_POLL_MS } from "./orders";
import { type Id, type Version } from "./types/common";
import {
  type AssignRiderRequest,
  type CashCollectedRequest,
  type ChangeSlotRequest,
  type CreateRiderRequest,
  type CreateSlotTemplateRequest,
  type DeliverShipmentRequest,
  type DeliverySlot,
  type DispatchShipmentRequest,
  type RecordAttemptRequest,
  type Rider,
  type Shipment,
  type SlotTemplate,
  type UpdateRiderRequest,
  type UpdateSlotTemplateRequest,
} from "./types/delivery";

// docs/api/README.md §4.17 — slot templates, slots, riders, shipments (assign, dispatch, deliver, attempts,
// reattempt, return) and cash collected (implemented by Payments, D14).

export const deliveryKeys = {
  all: ["delivery"] as const,
  templates: (q: object) => ["delivery", "templates", q] as const,
  slots: (q: object) => ["delivery", "slots", q] as const,
  riders: (q: object) => ["delivery", "riders", q] as const,
  shipments: (q: object) => ["delivery", "shipments", q] as const,
  shipment: (id: Id) => ["delivery", "shipment", id] as const,
};

type Query = Record<string, string | number | boolean | string[] | undefined>;

export function useSlotTemplates(query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: deliveryKeys.templates(query),
    queryFn: ({ signal }) =>
      http.get<Page<SlotTemplate>>("/admin/delivery/slot-templates", { query, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useSlots(query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: deliveryKeys.slots(query),
    queryFn: ({ signal }) =>
      http.get<Page<DeliverySlot>>("/admin/delivery/slots", { query, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useRiders(query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: deliveryKeys.riders(query),
    queryFn: ({ signal }) => http.get<Page<Rider>>("/admin/delivery/riders", { query, signal }),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useShipments(query: Query, enabled = true) {
  const http = useHttp();
  return useQuery({
    queryKey: deliveryKeys.shipments(query),
    queryFn: ({ signal }) =>
      http.get<Page<Shipment>>("/admin/delivery/shipments", { query, signal }),
    placeholderData: keepPreviousData,
    // AD-7: new and changed shipments appear while the board is open.
    refetchInterval: WORK_LIST_POLL_MS,
    enabled,
  });
}

export function useShipment(id: Id) {
  const http = useHttp();
  return useQuery({
    queryKey: deliveryKeys.shipment(id),
    queryFn: ({ signal }) => http.get<Shipment>(`/admin/delivery/shipments/${id}`, { signal }),
  });
}

/**
 * Re-reads one shipment after a 412 (AD-10): the fresh copy goes into the detail cache and the board is refreshed,
 * and the caller gets it to retry with its new version.
 */
export function useReloadShipment() {
  const http = useHttp();
  const qc = useQueryClient();
  return async (id: Id): Promise<Shipment> => {
    const fresh = await qc.fetchQuery({
      queryKey: deliveryKeys.shipment(id),
      queryFn: ({ signal }) => http.get<Shipment>(`/admin/delivery/shipments/${id}`, { signal }),
      staleTime: 0,
    });
    await qc.invalidateQueries({ queryKey: ["delivery", "shipments"] });
    return fresh;
  };
}

function useDeliveryWrite<TVars, TResult>(
  run: (http: ReturnType<typeof useHttp>, vars: TVars) => Promise<TResult>,
) {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => run(http, vars),
    onSuccess: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: deliveryKeys.all }),
        qc.invalidateQueries({ queryKey: ["orders"] }),
        qc.invalidateQueries({ queryKey: ["reports", "dashboard"] }),
      ]),
  });
}

export function useCreateSlotTemplate() {
  return useDeliveryWrite((http, body: CreateSlotTemplateRequest) =>
    http.post<SlotTemplate>("/admin/delivery/slot-templates", body),
  );
}

export function useUpdateSlotTemplate() {
  return useDeliveryWrite(
    (http, { id, body, version }: { id: Id; body: UpdateSlotTemplateRequest; version: Version }) =>
      http.put<SlotTemplate>(`/admin/delivery/slot-templates/${id}`, body, { ifMatch: version }),
  );
}

export function useChangeSlot() {
  return useDeliveryWrite(
    (http, { id, body, version }: { id: Id; body: ChangeSlotRequest; version: Version }) =>
      http.patch<DeliverySlot>(`/admin/delivery/slots/${id}`, body, { ifMatch: version }),
  );
}

export function useCreateRider() {
  return useDeliveryWrite((http, body: CreateRiderRequest) =>
    http.post<Rider>("/admin/delivery/riders", body),
  );
}

export function useUpdateRider() {
  return useDeliveryWrite(
    (http, { id, body, version }: { id: Id; body: UpdateRiderRequest; version: Version }) =>
      http.put<Rider>(`/admin/delivery/riders/${id}`, body, { ifMatch: version }),
  );
}

type ShipmentCommand<TBody> = { id: Id; version: Version; body: TBody };

export function useAssignRider() {
  return useDeliveryWrite((http, { id, version, body }: ShipmentCommand<AssignRiderRequest>) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/assign`, body, { ifMatch: version }),
  );
}

export function useDispatchShipment() {
  return useDeliveryWrite((http, { id, version, body }: ShipmentCommand<DispatchShipmentRequest>) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/dispatch`, body, { ifMatch: version }),
  );
}

export function useDeliverShipment() {
  return useDeliveryWrite((http, { id, version, body }: ShipmentCommand<DeliverShipmentRequest>) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/deliver`, body, { ifMatch: version }),
  );
}

export function useRecordAttempt() {
  return useDeliveryWrite((http, { id, version, body }: ShipmentCommand<RecordAttemptRequest>) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/attempts`, body, { ifMatch: version }),
  );
}

export function useReattempt() {
  return useDeliveryWrite((http, { id, version }: { id: Id; version: Version }) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/reattempt`, undefined, {
      ifMatch: version,
    }),
  );
}

export function useReturnShipment() {
  return useDeliveryWrite((http, { id, version }: { id: Id; version: Version }) =>
    http.post<Shipment>(`/admin/delivery/shipments/${id}/return`, undefined, { ifMatch: version }),
  );
}

/** Retryable write: the caller keeps one Idempotency-Key per user action (docs/api §1.8). */
export function useRecordCashCollected() {
  return useDeliveryWrite(
    (
      http,
      { id, body, idempotencyKey }: { id: Id; body: CashCollectedRequest; idempotencyKey: string },
    ) =>
      http.post<unknown>(`/admin/delivery/shipments/${id}/cash-collected`, body, {
        idempotencyKey,
      }),
  );
}
