import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { type ColumnDef, DataTable } from "./data-table";
import { FilterBar } from "./filter-bar";

interface OrderRow {
  id: string;
  orderNumber: string;
  totalPaise: number;
}

const columns: ColumnDef<OrderRow>[] = [
  { id: "orderNumber", accessorKey: "orderNumber", header: "Order" },
  { id: "totalPaise", accessorKey: "totalPaise", header: "Total", meta: { align: "right" } },
  { id: "note", header: "Note", enableSorting: false, cell: () => "—" },
];

const rows: OrderRow[] = [
  { id: "o1", orderNumber: "FO-260001", totalPaise: 64900 },
  { id: "o2", orderNumber: "FO-260002", totalPaise: 129800 },
];

const base = {
  columns,
  getRowId: (r: OrderRow) => r.id,
  getRowLabel: (r: OrderRow) => r.orderNumber,
  caption: "Orders",
  page: 1,
  pageSize: 20,
  totalCount: 2,
  onPageChange: () => {},
};

describe("DataTable", () => {
  it("renders rows and reports server sorting changes with aria-sort", async () => {
    const onSortingChange = vi.fn();
    const { rerender } = render(
      <DataTable {...base} data={rows} sorting={[]} onSortingChange={onSortingChange} />,
    );

    expect(screen.getByRole("table", { name: "Orders" })).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(3);
    const orderHeader = screen.getByRole("columnheader", { name: /Order/ });
    expect(orderHeader).toHaveAttribute("aria-sort", "none");
    expect(screen.getByRole("columnheader", { name: "Note" })).not.toHaveAttribute("aria-sort");

    await userEvent.click(screen.getByRole("button", { name: /Order/ }));
    expect(onSortingChange).toHaveBeenCalledWith([{ id: "orderNumber", desc: false }]);

    rerender(
      <DataTable
        {...base}
        data={rows}
        sorting={[{ id: "orderNumber", desc: true }]}
        onSortingChange={onSortingChange}
      />,
    );
    expect(screen.getByRole("columnheader", { name: /Order/ })).toHaveAttribute(
      "aria-sort",
      "descending",
    );
    await expectNoAxeViolations();
  });

  it("selects rows and all rows on the page", async () => {
    const onRowSelectionChange = vi.fn();
    const { rerender } = render(
      <DataTable
        {...base}
        data={rows}
        enableRowSelection
        rowSelection={{}}
        onRowSelectionChange={onRowSelectionChange}
      />,
    );
    await userEvent.click(screen.getByRole("checkbox", { name: "Select FO-260002" }));
    expect(onRowSelectionChange).toHaveBeenLastCalledWith({ o2: true });

    await userEvent.click(screen.getByRole("checkbox", { name: "Select all rows on this page" }));
    expect(onRowSelectionChange).toHaveBeenLastCalledWith({ o1: true, o2: true });

    rerender(
      <DataTable
        {...base}
        data={rows}
        enableRowSelection
        rowSelection={{ o1: true }}
        onRowSelectionChange={onRowSelectionChange}
        selectionActions={(ids) => <button type="button">Print {ids.length} pick lists</button>}
      />,
    );
    expect(screen.getByRole("status")).toHaveTextContent("1 selected");
    expect(screen.getByRole("button", { name: "Print 1 pick lists" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Select all rows on this page" })).toHaveAttribute(
      "aria-checked",
      "mixed",
    );
  });

  it("shows loading skeleton, empty and error states", async () => {
    const { rerender, container } = render(
      <DataTable {...base} data={[]} totalCount={0} isLoading />,
    );
    expect(screen.getByRole("table")).toHaveAttribute("aria-busy", "true");
    expect(container.querySelectorAll(".animate-pulse").length).toBeGreaterThan(0);

    rerender(<DataTable {...base} data={[]} totalCount={0} />);
    expect(screen.getByText("Nothing here yet")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Pagination" })).not.toBeInTheDocument();

    const onRetry = vi.fn();
    rerender(
      <DataTable
        {...base}
        data={[]}
        totalCount={0}
        error={{ traceId: "00-x-01" }}
        onRetry={onRetry}
      />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Could not load this list");
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetry).toHaveBeenCalled();
  });

  it("drives server paging", async () => {
    const onPageChange = vi.fn();
    render(<DataTable {...base} data={rows} totalCount={45} onPageChange={onPageChange} />);
    await userEvent.click(screen.getByRole("button", { name: "Page 3" }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });
});

describe("FilterBar", () => {
  it("reports search input and removes chips", async () => {
    const onSearch = vi.fn();
    const onRemove = vi.fn();
    const onClearAll = vi.fn();
    render(
      <FilterBar
        search={{ value: "", onChange: onSearch, label: "Search orders" }}
        activeFilters={[{ id: "status", label: "Status: Confirmed", onRemove }]}
        onClearAll={onClearAll}
      />,
    );
    expect(screen.getByRole("search", { name: "Filters" })).toBeInTheDocument();
    await userEvent.type(screen.getByRole("searchbox", { name: "Search orders" }), "F");
    expect(onSearch).toHaveBeenCalledWith("F");
    await userEvent.click(screen.getByRole("button", { name: "Remove filter Status: Confirmed" }));
    expect(onRemove).toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Clear all" }));
    expect(onClearAll).toHaveBeenCalled();
    await expectNoAxeViolations();
  });
});
