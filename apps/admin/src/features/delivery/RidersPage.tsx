import { isApiError } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useCreateRider, useRiders, useUpdateRider } from "../../api/delivery";
import { type Rider } from "../../api/types/delivery";
import { useLocationScope } from "../../app/location-scope";
import { SwitchField, TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { errorMessage } from "../../lib/errors";
import { notifySaved } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { ACTIVE_FLAG } from "../../lib/status";
import { DeliveryNav, FormAlert, reportWriteError } from "./shared";

const FILTERS = ["isActive"] as const;
const ANY = "__any";

export function RidersPage() {
  const list = useListParams();
  const scope = useLocationScope();
  const [editing, setEditing] = useState<Rider | "new" | null>(null);
  const riders = useRiders({
    ...list.query(FILTERS),
    ...(scope.locationId ? { fulfillmentLocationId: scope.locationId } : {}),
  });

  const columns: ColumnDef<Rider>[] = [
    {
      id: "name",
      header: "Name",
      cell: ({ row }) => <span className="font-medium">{row.original.fullName}</span>,
    },
    { id: "phone", header: "Phone", cell: ({ row }) => row.original.phone },
    ...(scope.locationId
      ? []
      : [
          {
            id: "location",
            header: "Location",
            cell: ({ row }) => scope.nameOf(row.original.fulfillmentLocationId),
          } satisfies ColumnDef<Rider>,
        ]),
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge code={String(row.original.isActive)} map={ACTIVE_FLAG} />,
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => (
        <Button
          size="sm"
          variant="outline"
          aria-label={`Edit ${row.original.fullName}`}
          onClick={() => setEditing(row.original)}
        >
          Edit
        </Button>
      ),
    },
  ];

  const isActive = list.get("isActive");
  return (
    <>
      <PageHeader
        title="Delivery"
        description={`Riders · ${scope.nameOf(scope.locationId)}`}
        actions={
          <Button
            onClick={() => setEditing("new")}
            disabled={!scope.locationId}
            title={scope.locationId ? undefined : "Choose a location in the top bar first."}
          >
            <Plus aria-hidden="true" />
            New rider
          </Button>
        }
      />
      <DeliveryNav />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={
            isActive
              ? [
                  {
                    id: "isActive",
                    label: `Status: ${ACTIVE_FLAG[isActive]?.label ?? isActive}`,
                    onRemove: () => list.set({ isActive: null }),
                  },
                ]
              : []
          }
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-48">
            <Select
              aria-label="Status"
              options={[
                { value: ANY, label: "Status: any" },
                { value: "true", label: "Active" },
                { value: "false", label: "Inactive" },
              ]}
              value={isActive ?? ANY}
              onValueChange={(v) => list.set({ isActive: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Riders"
          columns={columns}
          data={riders.data?.items ?? []}
          getRowId={(r) => r.id}
          getRowLabel={(r) => r.fullName}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={riders.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={riders.isFetching}
          error={tableError(riders.error)}
          onRetry={() => void riders.refetch()}
          emptyState={
            <EmptyState
              title="No riders"
              description="Add the riders who deliver from this location."
            />
          }
        />
      </div>
      {editing && (
        <RiderDialog
          locationId={scope.locationId}
          rider={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </>
  );
}

const riderSchema = z.object({
  fullName: z.string().trim().min(1, "Enter the rider's name.").max(120),
  phone: z
    .string()
    .trim()
    .regex(/^\+[1-9]\d{7,14}$/, "Enter the phone with country code, e.g. +919876543210."),
  isActive: z.boolean(),
});

function RiderDialog({
  locationId,
  rider,
  onClose,
}: {
  locationId: string | null;
  rider: Rider | null;
  onClose: () => void;
}) {
  const create = useCreateRider();
  const update = useUpdateRider();
  const [refusal, setRefusal] = useState<string | null>(null);
  const form = useForm<z.infer<typeof riderSchema>>({
    resolver: zodResolver(riderSchema),
    defaultValues: {
      fullName: rider?.fullName ?? "",
      phone: rider?.phone ?? "+91",
      isActive: rider?.isActive ?? true,
    },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    setRefusal(null);
    try {
      if (rider) {
        await update.mutateAsync({ id: rider.id, version: rider.version, body: values });
      } else if (locationId) {
        await create.mutateAsync({
          fulfillmentLocationId: locationId,
          fullName: values.fullName,
          phone: values.phone,
        });
      }
      notifySaved(rider ? "Rider saved" : "Rider added");
      onClose();
    } catch (error) {
      if (isApiError(error) && error.code === "RIDER_HAS_ACTIVE_SHIPMENTS") {
        setRefusal(errorMessage(error));
        return;
      }
      reportWriteError(error, {
        setError: form.setError,
        fields: ["fullName", "phone", "isActive"],
        codeFields: { DUPLICATE: "phone" },
        onConflict: onClose,
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title={rider ? `Edit ${rider.fullName}` : "New rider"}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="rider" loading={create.isPending || update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <form id="rider" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <FormAlert>{refusal}</FormAlert>
          <TextField form={form} name="fullName" label="Full name" required autoComplete="off" />
          <TextField
            form={form}
            name="phone"
            label="Phone"
            type="tel"
            required
            autoComplete="off"
          />
          {rider && (
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A rider with shipments out for delivery cannot be deactivated."
            />
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}
