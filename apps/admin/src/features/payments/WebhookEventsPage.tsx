import { formatDateTime } from "@fresh/core";
import { Badge, Button, type ColumnDef, DataTable, FilterBar, StatusBadge } from "@fresh/ui";
import { useReprocessWebhookEvent, useWebhookEvents } from "../../api/payments";
import { type WebhookEvent } from "../../api/types/payments";
import { useCan } from "../../app/services";
import { useLocationScope } from "../../app/location-scope";
import { NoAccess, PageHeader, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { WEBHOOK_STATUS, statusOptions } from "../../lib/status";
import { FilterSelect, PaymentsTabs, activeFilters } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["status", "provider", "signatureValid", "processed"] as const;

const YES_NO = [
  { value: "true", label: "Yes" },
  { value: "false", label: "No" },
];

function ReprocessButton({ event }: { event: WebhookEvent }) {
  const reprocess = useReprocessWebhookEvent();
  return (
    <Button
      size="sm"
      variant="outline"
      loading={reprocess.isPending}
      onClick={() =>
        reprocess.mutate(event.id, {
          onSuccess: () => notifySaved("Event reprocessed"),
          onError: (error) => reportError(error, { title: "Could not reprocess the event" }),
        })
      }
    >
      Reprocess
    </Button>
  );
}

/** Provider webhook inbox (all-locations staff only); events in ERROR can be reprocessed with payments.manage. */
export function WebhookEventsPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const events = useWebhookEvents(list.query(FILTERS), scope.allLocations);
  const canManage = can("payments.manage");

  if (!scope.allLocations) return <NoAccess />;

  const columns: ColumnDef<WebhookEvent>[] = [
    {
      id: "received",
      header: "Received",
      cell: ({ row }) => formatDateTime(row.original.receivedAt),
    },
    { id: "provider", header: "Provider", cell: ({ row }) => row.original.provider },
    {
      id: "event",
      header: "Event",
      cell: ({ row }) => (
        <span>
          {row.original.eventType}
          <span className="text-text-muted block font-mono text-xs">
            {row.original.providerEventId}
          </span>
        </span>
      ),
    },
    {
      id: "signature",
      header: "Signature",
      cell: ({ row }) =>
        row.original.signatureValid ? (
          <Badge tone="success">Valid</Badge>
        ) : (
          <Badge tone="danger">Invalid</Badge>
        ),
    },
    {
      id: "payload",
      header: "Payload",
      cell: ({ row }) => (
        <span className="flex flex-col gap-1">
          <span
            className="text-text-muted font-mono text-xs"
            title={`SHA-256 ${row.original.payloadSha256}`}
          >
            {row.original.payloadSha256.slice(0, 12)}
          </span>
          {row.original.payloadTruncated && <Badge tone="warning">Truncated</Badge>}
        </span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => (
        <span>
          <StatusBadge code={row.original.status} map={WEBHOOK_STATUS} />
          {row.original.error && (
            <span className="text-danger-text block text-xs">{row.original.error}</span>
          )}
        </span>
      ),
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      cell: ({ row }) =>
        canManage && row.original.status === "ERROR" ? (
          <ReprocessButton event={row.original} />
        ) : null,
    },
  ];

  return (
    <>
      <PageHeader title="Payments" description="Provider webhook events received by the API" />
      <PaymentsTabs current="webhooks" showWebhooks />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={activeFilters(list, [
            { name: "status", label: "Status" },
            { name: "provider", label: "Provider" },
            { name: "signatureValid", label: "Signature valid" },
            { name: "processed", label: "Processed" },
          ])}
          onClearAll={() => list.clear(FILTERS)}
        >
          <FilterSelect
            list={list}
            name="status"
            label="Status"
            choices={statusOptions(WEBHOOK_STATUS)}
          />
          <FilterSelect
            list={list}
            name="signatureValid"
            label="Signature valid"
            choices={YES_NO}
          />
          <FilterSelect list={list} name="processed" label="Processed" choices={YES_NO} />
        </FilterBar>
        <DataTable
          caption="Webhook events"
          columns={columns}
          data={events.data?.items ?? []}
          getRowId={(e) => e.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={events.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={events.isFetching}
          error={tableError(events.error)}
          onRetry={() => void events.refetch()}
          emptyState={<ListEmpty title="No webhook events match these filters" />}
        />
      </div>
    </>
  );
}
