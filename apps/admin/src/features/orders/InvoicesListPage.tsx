import { formatCalendarDate, formatRupees } from "@fresh/core";
import { type ActiveFilter, type ColumnDef, DataTable, FilterBar, Input } from "@fresh/ui";
import { useMemo } from "react";
import { Link } from "react-router";
import { useInvoices } from "../../api/orders";
import { type AdminInvoiceSummary } from "../../api/types/orders";
import { useLocationScope } from "../../app/location-scope";
import { PageHeader, tableError } from "../../components/page";
import { ListEmpty } from "../../components/states";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { InvoicePdfDownload, PdfNotReady } from "./InvoicePdf";

const FILTER_KEYS = ["fromDate", "toDate"] as const;

/**
 * The invoice register (MF-2, MF-8, D353–D355): every GST invoice issued at the staff member's locations, newest
 * first, from `GET /admin/invoices`. Dates are the invoice dates the server prints; every amount is the server's.
 */
export function InvoicesListPage() {
  const list = useListParams({});
  const search = useDebouncedSearch(list);
  const { locationId, nameOf } = useLocationScope();
  const query = {
    ...list.query(FILTER_KEYS),
    ...(locationId ? { fulfillmentLocationId: locationId } : {}),
  };
  const invoices = useInvoices(query);

  const columns = useMemo<ColumnDef<AdminInvoiceSummary>[]>(
    () => [
      {
        id: "invoiceNumber",
        header: "Invoice",
        enableSorting: false,
        cell: ({ row }) => (
          <Link
            to={`/orders/${row.original.orderId}/invoice`}
            className="text-accent-text font-medium hover:underline"
          >
            {row.original.invoiceNumber}
          </Link>
        ),
      },
      {
        id: "invoiceDate",
        header: "Date",
        enableSorting: false,
        cell: ({ row }) => formatCalendarDate(row.original.invoiceDate),
      },
      {
        id: "orderNumber",
        header: "Order",
        enableSorting: false,
        cell: ({ row }) => (
          <Link to={`/orders/${row.original.orderId}`} className="hover:underline">
            {row.original.orderNumber}
          </Link>
        ),
      },
      {
        id: "customerName",
        header: "Customer",
        enableSorting: false,
        cell: ({ row }) => row.original.customerName,
      },
      ...(locationId
        ? []
        : [
            {
              id: "location",
              header: "Location",
              enableSorting: false,
              cell: ({ row }: { row: { original: AdminInvoiceSummary } }) =>
                nameOf(row.original.fulfillmentLocationId),
            } satisfies ColumnDef<AdminInvoiceSummary>,
          ]),
      {
        id: "taxablePaise",
        header: "Taxable value",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.taxablePaise),
      },
      {
        id: "taxPaise",
        header: "GST",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.taxPaise),
      },
      {
        id: "totalPaise",
        header: "Total",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.totalPaise),
      },
      {
        id: "pdf",
        header: "PDF",
        enableSorting: false,
        cell: ({ row }) =>
          row.original.pdfUrl ? (
            <InvoicePdfDownload
              path={row.original.pdfUrl}
              invoiceNumber={row.original.invoiceNumber}
            />
          ) : (
            <PdfNotReady />
          ),
      },
    ],
    [locationId, nameOf],
  );

  const fromDate = list.get("fromDate");
  const toDate = list.get("toDate");
  const active: ActiveFilter[] = [
    ...(fromDate
      ? [
          {
            id: "fromDate",
            label: `From ${fromDate}`,
            onRemove: () => list.set({ fromDate: null }),
          },
        ]
      : []),
    ...(toDate
      ? [{ id: "toDate", label: `To ${toDate}`, onRemove: () => list.set({ toDate: null }) }]
      : []),
  ];

  const scope = locationId ? nameOf(locationId) : "All your locations";

  return (
    <>
      <PageHeader title="Invoices" description={`GST invoices issued at delivery · ${scope}`} />
      <div className="flex flex-col gap-4">
        <FilterBar
          search={{
            ...search,
            placeholder: "Invoice or order number",
            label: "Search invoices",
          }}
          activeFilters={active}
          onClearAll={() => list.clear([...FILTER_KEYS, "q"])}
        >
          <div className="w-44">
            <Input
              type="date"
              aria-label="From"
              title="First invoice date"
              value={fromDate ?? ""}
              max={toDate}
              onChange={(e) => list.set({ fromDate: e.target.value || null })}
            />
          </div>
          <div className="w-44">
            <Input
              type="date"
              aria-label="To"
              title="Last invoice date"
              value={toDate ?? ""}
              min={fromDate}
              onChange={(e) => list.set({ toDate: e.target.value || null })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Invoices"
          columns={columns}
          data={invoices.data?.items ?? []}
          getRowId={(i) => i.id}
          page={list.page}
          pageSize={list.pageSize}
          totalCount={invoices.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={invoices.isFetching}
          error={tableError(invoices.error)}
          onRetry={() => void invoices.refetch()}
          emptyState={
            <ListEmpty
              title="No invoices match these filters"
              description={
                active.length > 0 || list.q
                  ? "Clear the filters to see all invoices."
                  : "Invoices are issued when an order is delivered."
              }
            />
          }
        />
      </div>
    </>
  );
}
