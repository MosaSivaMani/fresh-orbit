import { formatDateTime } from "@fresh/core";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { z } from "zod";
import { useCities, useCreateCity } from "../../api/locations";
import { type City } from "../../api/types/locations";
import { TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved } from "../../lib/feedback";
import { useListParams } from "../../lib/list-params";
import { CITY_STATUS, statusOptions } from "../../lib/status";
import { STATE_NAME_HINT, stateNameField } from "./schemas";
import { reportWriteError } from "./shared";
import { ListEmpty } from "../../components/states";

const FILTERS = ["status"] as const;
const ANY = "__any";

const columns: ColumnDef<City>[] = [
  {
    id: "name",
    header: "City",
    cell: ({ row }) => (
      <Link
        to={`/cities/${row.original.id}`}
        className="text-accent-text font-medium hover:underline"
      >
        {row.original.name}
      </Link>
    ),
  },
  { id: "code", header: "Code", cell: ({ row }) => row.original.code },
  { id: "state", header: "State", cell: ({ row }) => row.original.stateCode },
  {
    id: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge code={row.original.status} map={CITY_STATUS} />,
  },
  {
    id: "activatedAt",
    header: "Activated",
    cell: ({ row }) => (row.original.activatedAt ? formatDateTime(row.original.activatedAt) : "—"),
  },
];

/** Cities the store serves or is preparing to serve (docs/design/admin.md, Cities & Locations). */
export function CitiesPage() {
  const list = useListParams();
  const cities = useCities(list.query(FILTERS));
  const [creating, setCreating] = useState(false);
  const status = list.get("status");

  return (
    <>
      <PageHeader
        title="Cities & Locations"
        description="A city goes live only when every launch check passes."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus aria-hidden="true" />
            New city
          </Button>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          activeFilters={
            status
              ? [
                  {
                    id: "status",
                    label: `Status: ${CITY_STATUS[status]?.label ?? status}`,
                    onRemove: () => list.set({ status: null }),
                  },
                ]
              : []
          }
          onClearAll={() => list.clear(FILTERS)}
        >
          <div className="w-48">
            <Select
              aria-label="Status"
              options={[{ value: ANY, label: "Status: any" }, ...statusOptions(CITY_STATUS)]}
              value={status ?? ANY}
              onValueChange={(v) => list.set({ status: v === ANY ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Cities"
          columns={columns}
          data={cities.data?.items ?? []}
          getRowId={(c) => c.id}
          getRowLabel={(c) => c.name}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={cities.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={cities.isFetching}
          error={tableError(cities.error)}
          onRetry={() => void cities.refetch()}
          emptyState={
            <ListEmpty title="No cities" description="Add the first city to start setting it up." />
          }
        />
      </div>
      {creating && <CreateCityDialog onClose={() => setCreating(false)} />}
    </>
  );
}

const citySchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^[A-Z][A-Z0-9_]{1,31}$/, "Enter a code in capitals, e.g. ABC."),
  name: z.string().trim().min(1, "Enter the city name.").max(120),
  stateCode: z
    .string()
    .trim()
    .regex(/^[A-Z]{2}$/, "Enter the two-letter state code, e.g. TG."),
  timeZone: z.string().trim().min(1, "Enter the IANA time zone, e.g. Asia/Kolkata."),
  stateName: stateNameField,
});

function CreateCityDialog({ onClose }: { onClose: () => void }) {
  const create = useCreateCity();
  const navigate = useNavigate();
  const form = useForm<z.infer<typeof citySchema>>({
    resolver: zodResolver(citySchema),
    defaultValues: { code: "", name: "", stateCode: "", stateName: "", timeZone: "Asia/Kolkata" },
  });
  const onSubmit = form.handleSubmit(async (values) => {
    try {
      // AD-9: the state name is sent (null when blank) so it prefills customer addresses.
      const city = await create.mutateAsync({ ...values, stateName: values.stateName || null });
      notifySaved("City added", "It stays in draft until every launch check passes.");
      onClose();
      void navigate(`/cities/${city.id}`);
    } catch (error) {
      reportWriteError(error, {
        setError: form.setError,
        fields: ["code", "name", "stateCode", "stateName", "timeZone"],
        codeFields: { DUPLICATE: "code" },
      });
    }
  });
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        size="sm"
        title="New city"
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="new-city" loading={create.isPending}>
              Add city
            </Button>
          </>
        }
      >
        <form id="new-city" noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <TextField form={form} name="name" label="Name" required />
          <div className="grid grid-cols-2 gap-4">
            <TextField form={form} name="code" label="Code" required autoComplete="off" />
            <TextField
              form={form}
              name="stateCode"
              label="State code"
              required
              autoComplete="off"
            />
          </div>
          <TextField
            form={form}
            name="stateName"
            label="State name"
            hint={STATE_NAME_HINT}
            autoComplete="off"
          />
          <TextField form={form} name="timeZone" label="Time zone" hint="IANA name." required />
        </form>
      </DialogContent>
    </Dialog>
  );
}
