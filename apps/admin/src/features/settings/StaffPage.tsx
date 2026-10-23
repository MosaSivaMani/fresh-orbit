import { formatDateTime } from "@fresh/core";
import {
  type ActiveFilter,
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  EmptyState,
  FilterBar,
  Select,
  StatusBadge,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@fresh/ui";
import { Plus, UserCog } from "lucide-react";
import { useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import { useStaffList } from "../../api/staff";
import { type Staff } from "../../api/types/staff";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { ACTIVE_FLAG } from "../../lib/status";
import { RolesPanel } from "./RolesPanel";
import { ResetPasswordDialog, StaffFormDialog } from "./StaffDialogs";

const FILTER_KEYS = ["isActive"] as const;
const ANY = "__any";
const USER_STATUS = {
  ACTIVE: { label: "Can sign in", tone: "success" },
  LOCKED: { label: "Locked", tone: "warning" },
  DISABLED: { label: "Disabled", tone: "danger" },
} as const;

type Dialog = { kind: "create" } | { kind: "edit"; staff: Staff } | { kind: "reset"; staff: Staff };

export function StaffPage() {
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") === "roles" ? "roles" : "staff";
  return (
    <>
      <PageHeader
        title="Staff & roles"
        description="Who can sign in to the admin and what they may do."
      />
      <Tabs
        value={tab}
        onValueChange={(v) => setParams(v === "roles" ? { tab: "roles" } : {}, { replace: true })}
        className="flex flex-col gap-4"
      >
        <TabsList>
          <TabsTrigger value="staff">Staff</TabsTrigger>
          <TabsTrigger value="roles">Roles</TabsTrigger>
        </TabsList>
        <TabsContent value="staff">{tab === "staff" && <StaffList />}</TabsContent>
        <TabsContent value="roles">{tab === "roles" && <RolesPanel />}</TabsContent>
      </Tabs>
    </>
  );
}

function StaffList() {
  const list = useListParams();
  const search = useDebouncedSearch(list);
  const staff = useStaffList(list.query(FILTER_KEYS));
  const { nameOf } = useLocationScope();
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const isActive = list.get("isActive");

  const columns = useMemo<ColumnDef<Staff>[]>(
    () => [
      {
        id: "fullName",
        header: "Name",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span className="font-medium">{row.original.fullName}</span>
            <span className="text-text-muted text-xs">{row.original.email ?? "—"}</span>
          </span>
        ),
      },
      {
        id: "roles",
        header: "Roles",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-wrap gap-1">
            {row.original.roles.map((r) => (
              <Badge key={r.id}>{r.name}</Badge>
            ))}
          </span>
        ),
      },
      {
        id: "locations",
        header: "Locations",
        enableSorting: false,
        cell: ({ row }) =>
          row.original.allLocations
            ? "All locations"
            : row.original.locationIds.map((id) => nameOf(id)).join(", ") || "—",
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex flex-wrap gap-1">
            <StatusBadge code={String(row.original.isActive)} map={ACTIVE_FLAG} />
            {row.original.isActive && row.original.userStatus !== "ACTIVE" && (
              <StatusBadge code={row.original.userStatus} map={USER_STATUS} />
            )}
          </span>
        ),
      },
      {
        id: "lastLoginAt",
        header: "Last sign-in",
        enableSorting: false,
        cell: ({ row }) =>
          row.original.lastLoginAt ? formatDateTime(row.original.lastLoginAt) : "Never",
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => (
          <span className="flex justify-end gap-1">
            <Button
              size="sm"
              variant="ghost"
              aria-label={`Edit ${row.original.fullName}`}
              onClick={() => setDialog({ kind: "edit", staff: row.original })}
            >
              Edit
            </Button>
            <Button
              size="sm"
              variant="ghost"
              aria-label={`Reset password for ${row.original.fullName}`}
              onClick={() => setDialog({ kind: "reset", staff: row.original })}
            >
              Reset password
            </Button>
          </span>
        ),
      },
    ],
    [nameOf],
  );

  const active: ActiveFilter[] = isActive
    ? [
        {
          id: "isActive",
          label: isActive === "true" ? "Active only" : "Inactive only",
          onRemove: () => list.set({ isActive: null }),
        },
      ]
    : [];

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        search={{ ...search, placeholder: "Name or email", label: "Search staff" }}
        activeFilters={active}
        onClearAll={() => list.clear([...FILTER_KEYS, "q"])}
        actions={
          <Button
            size="sm"
            leadingIcon={<Plus aria-hidden="true" />}
            onClick={() => setDialog({ kind: "create" })}
          >
            New staff member
          </Button>
        }
      >
        <div className="w-40">
          <Select
            aria-label="Active"
            value={isActive ?? ANY}
            options={[
              { value: ANY, label: "Active and inactive" },
              { value: "true", label: "Active" },
              { value: "false", label: "Inactive" },
            ]}
            onValueChange={(v) => list.set({ isActive: v === ANY ? null : v })}
          />
        </div>
      </FilterBar>
      <DataTable
        caption="Staff"
        columns={columns}
        data={staff.data?.items ?? []}
        getRowId={(s) => s.id}
        getRowLabel={(s) => s.fullName}
        page={list.page}
        pageSize={list.pageSize}
        totalCount={staff.data?.totalCount ?? 0}
        onPageChange={list.setPage}
        onPageSizeChange={list.setPageSize}
        isLoading={staff.isFetching}
        error={tableError(staff.error)}
        onRetry={() => void staff.refetch()}
        emptyState={
          <EmptyState
            icon={<UserCog aria-hidden="true" />}
            title="No staff match"
            description={list.q || isActive ? "Clear the filters to see everyone." : undefined}
          />
        }
      />
      {dialog?.kind === "create" && <StaffFormDialog onClose={() => setDialog(null)} />}
      {dialog?.kind === "edit" && (
        <StaffFormDialog staff={dialog.staff} onClose={() => setDialog(null)} />
      )}
      {dialog?.kind === "reset" && (
        <ResetPasswordDialog staff={dialog.staff} onClose={() => setDialog(null)} />
      )}
    </div>
  );
}
