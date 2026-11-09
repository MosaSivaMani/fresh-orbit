import { formatDateTime } from "@fresh/core";
import {
  type ActiveFilter,
  Button,
  type ColumnDef,
  DataTable,
  DetailList,
  Dialog,
  DialogContent,
  FilterBar,
  Select,
  Skeleton,
  StatusBadge,
} from "@fresh/ui";
import { useMemo, useState } from "react";
import { useDeadLetter, useDeadLetters, useReplayDeadLetter } from "../../api/operations";
import { type DeadLetter, type DeadLetterStatus } from "../../api/types/operations";
import { PageHeader, tableError } from "../../components/page";
import { AllCaughtUp, ListEmpty } from "../../components/states";
import { notifySaved, reportError } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { DEAD_LETTER_STATUS, eventName, handlerName } from "./shared";

// D357 (audit BF-2): failed outbox messages for operations staff (`platform.manage`). List, details and a confirmed
// replay that puts the event back in the queue (D296). Open letters are shown first by default.

const ALL = "ALL";
const STATUS_OPTIONS = [
  { value: "OPEN", label: "Open" },
  { value: "REPLAYED", label: "Replayed" },
  { value: ALL, label: "All" },
];

export function DeadLettersPage() {
  const list = useListParams();
  const raw = list.get("status");
  const status: DeadLetterStatus | undefined =
    raw === ALL ? undefined : raw === "REPLAYED" ? "REPLAYED" : "OPEN";
  const selected = status ?? ALL;
  const letters = useDeadLetters({
    page: list.page,
    pageSize: list.pageSize,
    ...(status ? { status } : {}),
  });
  const [openId, setOpenId] = useState<string | undefined>(undefined);

  const columns = useMemo<ColumnDef<DeadLetter>[]>(
    () => [
      {
        id: "failedAt",
        header: "Failed",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.failedAt),
      },
      {
        id: "event",
        header: "Event",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span className="font-medium">{eventName(row.original.eventType)}</span>
            <span className="text-text-muted text-xs">
              Raised {formatDateTime(row.original.eventOccurredAt)}
            </span>
          </span>
        ),
      },
      {
        id: "handler",
        header: "Handler",
        enableSorting: false,
        cell: ({ row }) => handlerName(row.original.handler),
      },
      {
        id: "attempts",
        header: "Attempts",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => row.original.attempts,
      },
      {
        id: "error",
        header: "Error",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="block max-w-md text-sm break-all">{row.original.error}</span>
        ),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <StatusBadge
            code={row.original.replayedAt ? "REPLAYED" : "OPEN"}
            map={DEAD_LETTER_STATUS}
          />
        ),
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        cell: ({ row }) => (
          <Button
            variant="outline"
            size="sm"
            aria-label={`Details of ${rowLabel(row.original)}`}
            onClick={() => setOpenId(row.original.id)}
          >
            Details
          </Button>
        ),
      },
    ],
    [],
  );

  const active: ActiveFilter[] = [];
  if (selected !== "OPEN") {
    const label = STATUS_OPTIONS.find((o) => o.value === selected)?.label ?? selected;
    active.push({
      id: "status",
      label: `Status: ${label}`,
      onRemove: () => list.set({ status: null }),
    });
  }

  return (
    <>
      <PageHeader
        title="Failed messages"
        description="Background work that failed after every retry (an invoice, a message, a stock update). Fix the cause, then replay the event."
      />
      <div className="flex flex-col gap-4">
        <FilterBar activeFilters={active} onClearAll={() => list.set({ status: null })}>
          <div className="w-44">
            <Select
              aria-label="Status"
              value={selected}
              options={STATUS_OPTIONS}
              onValueChange={(v) => list.set({ status: v === "OPEN" ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Failed messages"
          columns={columns}
          data={letters.data?.items ?? []}
          getRowId={(d) => d.id}
          getRowLabel={rowLabel}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={letters.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={letters.isFetching}
          error={tableError(letters.error)}
          onRetry={() => void letters.refetch()}
          emptyState={
            status === "OPEN" ? (
              <AllCaughtUp
                title="No failed messages"
                description="Every background event was handled."
              />
            ) : (
              <ListEmpty title="No failed messages match" />
            )
          }
        />
      </div>
      {openId && <DeadLetterDialog id={openId} onClose={() => setOpenId(undefined)} />}
    </>
  );
}

function rowLabel(letter: DeadLetter): string {
  return `${eventName(letter.eventType)} for ${handlerName(letter.handler)}`;
}

function Code({ children }: { children: string }) {
  return <span className="font-mono text-xs">{children}</span>;
}

/** Details of one dead letter; Replay asks for confirmation in the same dialog (no alert boxes). */
function DeadLetterDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const letter = useDeadLetter(id);
  const replay = useReplayDeadLetter();
  const [confirming, setConfirming] = useState(false);
  const data = letter.data;
  const isOpen = data !== undefined && data.replayedAt === null;

  const footer = confirming ? (
    <>
      <Button variant="outline" onClick={() => setConfirming(false)}>
        Keep as is
      </Button>
      <Button
        loading={replay.isPending}
        onClick={() =>
          replay.mutate(id, {
            onSuccess: () => {
              notifySaved("Event put back in the queue", "It is handled again within a minute.");
              onClose();
            },
            onError: (error) => {
              setConfirming(false);
              reportError(error, { title: "Could not replay" });
              void letter.refetch();
            },
          })
        }
      >
        Replay now
      </Button>
    </>
  ) : (
    <>
      <Button variant="outline" onClick={onClose}>
        Close
      </Button>
      {isOpen && <Button onClick={() => setConfirming(true)}>Replay</Button>}
    </>
  );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        size="lg"
        title={confirming ? "Replay this event?" : "Failed message"}
        description={
          confirming
            ? "The event goes back in the queue and every handler that failed runs again. Handlers that already succeeded do not run twice. Replay only after the cause is fixed."
            : undefined
        }
        footer={footer}
      >
        {letter.isPending ? (
          <div aria-busy="true" className="flex flex-col gap-2">
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : letter.error || !data ? (
          <p className="text-danger-text text-sm">This failed message could not be loaded.</p>
        ) : confirming ? (
          <DetailList
            columns={1}
            items={[
              { label: "Event", value: eventName(data.eventType) },
              { label: "Handler", value: handlerName(data.handler) },
            ]}
          />
        ) : (
          <div className="flex flex-col gap-4">
            <DetailList
              items={[
                {
                  label: "Status",
                  value: (
                    <StatusBadge
                      code={data.replayedAt ? "REPLAYED" : "OPEN"}
                      map={DEAD_LETTER_STATUS}
                    />
                  ),
                },
                { label: "Event", value: eventName(data.eventType) },
                { label: "Handler", value: handlerName(data.handler) },
                { label: "Attempts", value: String(data.attempts) },
                { label: "Event raised", value: formatDateTime(data.eventOccurredAt) },
                { label: "Failed", value: formatDateTime(data.failedAt) },
                {
                  label: "Replayed",
                  value: data.replayedAt ? formatDateTime(data.replayedAt) : null,
                },
                { label: "Correlation id", value: data.correlationId },
                { label: "Event id", value: <Code>{data.eventId}</Code> },
                { label: "Event type", value: <Code>{data.eventType}</Code> },
              ]}
            />
            <div className="flex flex-col gap-1">
              <span className="text-text-muted text-xs font-bold tracking-[0.08em] uppercase">
                Error
              </span>
              <pre className="bg-muted max-h-64 overflow-auto rounded-md p-2 text-xs whitespace-pre-wrap">
                {data.error}
              </pre>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
