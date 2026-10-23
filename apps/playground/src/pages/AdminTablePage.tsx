import { type Page, formatDateTime, formatRupees, formatWeight } from "@fresh/core";
import {
  AppShell,
  Button,
  type ColumnDef,
  DataTable,
  FilterBar,
  IconButton,
  type RowSelectionState,
  Select,
  type SortingState,
  StatusBadge,
  Switch,
} from "@fresh/ui";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  Bell,
  Boxes,
  ClipboardList,
  Download,
  Fish,
  LayoutDashboard,
  MapPin,
  Printer,
  Settings,
  Truck,
} from "lucide-react";
import { useMemo, useState } from "react";
import { ORDER_STATUS } from "../status";

interface OrderRow {
  id: string;
  orderNumber: string;
  customer: string;
  status: string;
  placedAt: string;
  slot: string;
  weightGrams: number;
  totalPaise: number;
}

// ---- Fake server (playground only): real apps call the API and get the same Page<T> envelope. ----
const names = [
  "Priya R",
  "Karthik S",
  "Anitha M",
  "Rahul V",
  "Divya K",
  "Suresh P",
  "Meena L",
  "Arjun T",
];
const statuses = Object.keys(ORDER_STATUS);
const ALL_ORDERS: OrderRow[] = Array.from({ length: 57 }, (_, i) => ({
  id: `o${i + 1}`,
  orderNumber: `FO-26${String(i + 1).padStart(4, "0")}`,
  customer: names[i % names.length] ?? "Customer",
  status: statuses[(i * 7) % statuses.length] ?? "CONFIRMED",
  placedAt: new Date(Date.UTC(2026, 9, 23, 1, 0) - i * 47 * 60_000).toISOString(),
  slot: i % 3 === 0 ? "07:00–09:00" : i % 3 === 1 ? "09:00–11:00" : "17:00–19:00",
  weightGrams: 250 * ((i % 8) + 1),
  totalPaise: 24900 + ((i * 3517) % 180000),
}));

interface OrderQuery {
  page: number;
  pageSize: number;
  sorting: SortingState;
  q: string;
  status: string;
  fail: boolean;
}

async function fetchOrders(query: OrderQuery): Promise<Page<OrderRow>> {
  await new Promise((r) => setTimeout(r, 450));
  if (query.fail)
    throw Object.assign(new Error("Simulated failure"), { traceId: "00-playground-01" });
  let rows = ALL_ORDERS.filter(
    (o) =>
      (query.status === "ALL" || o.status === query.status) &&
      (query.q === "" ||
        `${o.orderNumber} ${o.customer}`.toLowerCase().includes(query.q.toLowerCase())),
  );
  const sort = query.sorting[0];
  if (sort) {
    const key = sort.id as keyof OrderRow;
    rows = [...rows].sort(
      (a, b) => (a[key] < b[key] ? -1 : a[key] > b[key] ? 1 : 0) * (sort.desc ? -1 : 1),
    );
  }
  const start = (query.page - 1) * query.pageSize;
  return {
    items: rows.slice(start, start + query.pageSize),
    page: query.page,
    pageSize: query.pageSize,
    totalCount: rows.length,
  };
}
// ---------------------------------------------------------------------------------------------------

const columns: ColumnDef<OrderRow>[] = [
  {
    id: "orderNumber",
    accessorKey: "orderNumber",
    header: "Order",
    cell: ({ row }) => (
      <span className="text-text font-medium whitespace-nowrap">{row.original.orderNumber}</span>
    ),
  },
  { id: "customer", accessorKey: "customer", header: "Customer" },
  {
    id: "status",
    accessorKey: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge code={row.original.status} map={ORDER_STATUS} />,
  },
  {
    id: "placedAt",
    accessorKey: "placedAt",
    header: "Placed",
    cell: ({ row }) => (
      <span className="whitespace-nowrap">{formatDateTime(row.original.placedAt)}</span>
    ),
  },
  { id: "slot", accessorKey: "slot", header: "Slot", enableSorting: false },
  {
    id: "weightGrams",
    accessorKey: "weightGrams",
    header: "Weight",
    meta: { align: "right" },
    cell: ({ row }) => formatWeight(row.original.weightGrams),
  },
  {
    id: "totalPaise",
    accessorKey: "totalPaise",
    header: "Total",
    meta: { align: "right" },
    cell: ({ row }) => formatRupees(row.original.totalPaise),
  },
];

const navigation = [
  {
    id: "main",
    items: [
      {
        id: "dashboard",
        label: "Dashboard",
        href: "#",
        icon: <LayoutDashboard aria-hidden="true" />,
      },
      {
        id: "orders",
        label: "Orders",
        href: "#",
        icon: <ClipboardList aria-hidden="true" />,
        active: true,
        badge: (
          <span className="bg-highlight text-on-highlight rounded-full px-2 text-xs font-semibold">
            12
          </span>
        ),
      },
      { id: "inventory", label: "Inventory", href: "#", icon: <Boxes aria-hidden="true" /> },
      { id: "products", label: "Products", href: "#", icon: <Fish aria-hidden="true" /> },
      { id: "delivery", label: "Delivery", href: "#", icon: <Truck aria-hidden="true" /> },
    ],
  },
  {
    id: "setup",
    label: "Setup",
    items: [
      { id: "cities", label: "Cities", href: "#", icon: <MapPin aria-hidden="true" /> },
      { id: "settings", label: "Settings", href: "#", icon: <Settings aria-hidden="true" /> },
    ],
  },
];

export function AdminTablePage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sorting, setSorting] = useState<SortingState>([{ id: "placedAt", desc: true }]);
  const [selection, setSelection] = useState<RowSelectionState>({});
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("ALL");
  const [fail, setFail] = useState(false);

  const query: OrderQuery = { page, pageSize, sorting, q, status, fail };
  const result = useQuery({
    queryKey: ["orders", query],
    queryFn: () => fetchOrders(query),
    placeholderData: keepPreviousData,
    retry: false,
  });

  const activeFilters = useMemo(
    () => [
      ...(status !== "ALL"
        ? [
            {
              id: "status",
              label: `Status: ${ORDER_STATUS[status]?.label ?? status}`,
              onRemove: () => setStatus("ALL"),
            },
          ]
        : []),
      ...(q ? [{ id: "q", label: `Search: ${q}`, onRemove: () => setQ("") }] : []),
    ],
    [status, q],
  );

  return (
    <div className="border-border h-[760px] overflow-hidden rounded-lg border">
      <AppShell
        embedded
        brand={
          <span className="font-display text-primary dark:text-accent text-lg font-bold">
            Fresh Orbit
          </span>
        }
        brandCollapsed={<Fish aria-hidden="true" className="text-accent size-6" />}
        navigation={navigation}
        topBarStart={<h2 className="truncate text-lg font-semibold">Orders · Velachery</h2>}
        topBarEnd={<IconButton label="Notifications" icon={<Bell aria-hidden="true" />} />}
      >
        <div className="flex flex-col gap-4">
          <FilterBar
            search={{
              value: q,
              onChange: (v) => {
                setQ(v);
                setPage(1);
              },
              label: "Search orders",
              placeholder: "Order number or customer",
            }}
            activeFilters={activeFilters}
            onClearAll={() => {
              setQ("");
              setStatus("ALL");
            }}
            actions={
              <>
                <Switch label="Simulate error" checked={fail} onCheckedChange={setFail} />
                <Button variant="outline" size="md" leadingIcon={<Download aria-hidden="true" />}>
                  Export CSV
                </Button>
              </>
            }
          >
            <div className="w-48">
              <Select
                aria-label="Status"
                value={status}
                onValueChange={(v) => {
                  setStatus(v);
                  setPage(1);
                }}
                options={[
                  { value: "ALL", label: "All statuses" },
                  ...Object.entries(ORDER_STATUS).map(([value, s]) => ({ value, label: s.label })),
                ]}
              />
            </div>
          </FilterBar>
          <DataTable
            caption="Orders"
            columns={columns}
            data={result.data?.items ?? []}
            getRowId={(r) => r.id}
            getRowLabel={(r) => r.orderNumber}
            page={result.data?.page ?? page}
            pageSize={pageSize}
            totalCount={result.data?.totalCount ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
            sorting={sorting}
            onSortingChange={(s) => {
              setSorting(s);
              setPage(1);
            }}
            enableRowSelection
            rowSelection={selection}
            onRowSelectionChange={setSelection}
            selectionActions={() => (
              <Button size="sm" variant="outline" leadingIcon={<Printer aria-hidden="true" />}>
                Print pick lists
              </Button>
            )}
            isLoading={result.isFetching}
            error={result.isError ? { traceId: "00-playground-01" } : null}
            onRetry={() => setFail(false)}
          />
        </div>
      </AppShell>
    </div>
  );
}
