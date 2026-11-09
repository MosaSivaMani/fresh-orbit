import { type Page } from "@fresh/core";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Id } from "./types/common";
import { type DeadLetter, type DeadLetterListQuery, type StoreReadiness } from "./types/operations";

// Failed messages (outbox dead letters, `platform.manage`, D296/D357) and store readiness (`settings.manage`, D358).

export const operationsKeys = {
  deadLetters: (q: object) => ["dead-letters", "list", q] as const,
  deadLetter: (id: Id) => ["dead-letters", "detail", id] as const,
  readiness: ["store-readiness"] as const,
};

export function useDeadLetters(query: DeadLetterListQuery) {
  const http = useHttp();
  return useQuery({
    queryKey: operationsKeys.deadLetters(query),
    queryFn: ({ signal }) =>
      http.get<Page<DeadLetter>>("/admin/dead-letters", { query: { ...query }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useDeadLetter(id: Id | undefined) {
  const http = useHttp();
  return useQuery({
    queryKey: operationsKeys.deadLetter(id ?? ""),
    queryFn: ({ signal }) => http.get<DeadLetter>(`/admin/dead-letters/${id}`, { signal }),
    enabled: id !== undefined,
  });
}

/** Puts the event back in the queue; every open dead letter of the same event is marked replayed. */
export function useReplayDeadLetter() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: Id) => http.post<DeadLetter>(`/admin/dead-letters/${id}/replay`, undefined),
    onSuccess: (data) => {
      qc.setQueryData(operationsKeys.deadLetter(data.id), data);
      void qc.invalidateQueries({ queryKey: ["dead-letters"] });
    },
  });
}

export function useStoreReadiness() {
  const http = useHttp();
  return useQuery({
    queryKey: operationsKeys.readiness,
    queryFn: ({ signal }) =>
      http.get<StoreReadiness>("/admin/settings/store-readiness", { signal }),
  });
}
