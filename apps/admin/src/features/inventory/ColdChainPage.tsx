import { zodResolver } from "@hookform/resolvers/zod";
import { formatDateTime, humanizeCode } from "@fresh/core";
import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  FilterBar,
  Select,
} from "@fresh/ui";
import { Thermometer } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useColdChain, useRecordTemperature } from "../../api/inventory";
import { type ColdChainLog } from "../../api/types/inventory";
import { useLocationScope } from "../../app/location-scope";
import { useCan } from "../../app/services";
import { SelectField, TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { istLocalToUtcIso } from "../../lib/units";
import { STORAGE_UNIT_OPTIONS } from "./maps";
import { InventoryNav } from "./shared";
import { ListEmpty } from "../../components/states";

const RANGE_OPTIONS = [
  { value: "all", label: "All readings" },
  { value: "true", label: "Out of range only" },
];

export function ColdChainPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const can = useCan();
  const outOfRangeOnly = list.get("outOfRangeOnly") === "true";
  const logs = useColdChain({
    ...list.query(["outOfRangeOnly"]),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });

  const columns: ColumnDef<ColdChainLog>[] = [
    { id: "when", header: "Recorded", cell: ({ row }) => formatDateTime(row.original.recordedAt) },
    ...(scope.locationId
      ? []
      : [
          {
            id: "location",
            header: "Location",
            cell: ({ row }) => scope.nameOf(row.original.fulfillmentLocationId),
          } satisfies ColumnDef<ColdChainLog>,
        ]),
    {
      id: "unit",
      header: "Storage unit",
      cell: ({ row }) => humanizeCode(row.original.storageUnit),
    },
    {
      id: "temperature",
      header: "Temperature",
      meta: { align: "right" },
      cell: ({ row }) => `${row.original.temperatureC} °C`,
    },
    {
      id: "range",
      header: "Range",
      cell: ({ row }) =>
        row.original.isOutOfRange ? (
          <Badge tone="danger">Out of range</Badge>
        ) : (
          <Badge tone="success">In range</Badge>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Cold chain"
        description="Temperature readings for chillers, freezers and receipts. The server decides what is out of range."
        actions={
          can("inventory.adjust") ? (
            <RecordTemperatureButton locationId={scope.locationId} />
          ) : undefined
        }
      />
      <InventoryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={
            outOfRangeOnly
              ? [
                  {
                    id: "range",
                    label: "Out of range only",
                    onRemove: () => list.set({ outOfRangeOnly: null }),
                  },
                ]
              : []
          }
          onClearAll={() => list.clear(["outOfRangeOnly"])}
        >
          <div className="w-48">
            <Select
              aria-label="Reading range"
              options={RANGE_OPTIONS}
              value={outOfRangeOnly ? "true" : "all"}
              onValueChange={(v) => list.set({ outOfRangeOnly: v === "true" ? "true" : null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Cold-chain readings"
          columns={columns}
          data={logs.data?.items ?? []}
          getRowId={(r) => r.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={logs.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={logs.isFetching}
          error={tableError(logs.error)}
          onRetry={() => void logs.refetch()}
          emptyState={
            <ListEmpty title="No readings" description="Record a temperature to start the log." />
          }
        />
      </div>
    </>
  );
}

const schema = z.object({
  storageUnit: z.string().min(1, "Choose the storage unit."),
  temperatureC: z
    .string()
    .trim()
    .regex(/^-?\d{1,2}(\.\d)?$/, "Enter the temperature in °C, for example -18 or 2.5."),
  recordedAt: z
    .string()
    .refine((v) => v === "" || istLocalToUtcIso(v) !== null, "Enter when the reading was taken."),
});

function RecordTemperatureButton({ locationId }: { locationId: string | null }) {
  const [open, setOpen] = useState(false);
  const record = useRecordTemperature();
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { storageUnit: "", temperatureC: "", recordedAt: "" },
  });
  const submit = form.handleSubmit(async (v) => {
    if (!locationId) return;
    try {
      const log = await record.mutateAsync({
        fulfillmentLocationId: locationId,
        storageUnit: v.storageUnit,
        temperatureC: Number(v.temperatureC),
        recordedAt: v.recordedAt ? istLocalToUtcIso(v.recordedAt) : null,
      });
      notifySaved(
        log.isOutOfRange ? "Reading recorded — out of range" : "Reading recorded",
        log.isOutOfRange ? "Check the unit and move stock if needed." : undefined,
      );
      form.reset();
      setOpen(false);
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: ["storageUnit", "temperatureC", "recordedAt"],
      });
    }
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <Button leadingIcon={<Thermometer aria-hidden="true" />} onClick={() => setOpen(true)}>
        Record temperature
      </Button>
      <DialogContent
        title="Record temperature"
        description="Readings can be back-dated up to 24 hours."
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="record-temperature"
              loading={record.isPending}
              disabled={!locationId}
            >
              Record
            </Button>
          </>
        }
      >
        {!locationId ? (
          <p className="text-text-muted text-sm">
            Choose a fulfillment location in the top bar first.
          </p>
        ) : (
          <form
            id="record-temperature"
            noValidate
            className="flex flex-col gap-4"
            onSubmit={(e) => {
              void submit(e);
            }}
          >
            <SelectField
              form={form}
              name="storageUnit"
              label="Storage unit"
              options={STORAGE_UNIT_OPTIONS}
              required
            />
            <TextField
              form={form}
              name="temperatureC"
              label="Temperature (°C)"
              inputMode="decimal"
              required
            />
            <TextField
              form={form}
              name="recordedAt"
              label="Taken at (IST)"
              hint="Leave empty for now."
              type="datetime-local"
            />
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
