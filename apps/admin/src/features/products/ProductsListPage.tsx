import { formatDate } from "@fresh/core";
import {
  Button,
  type ColumnDef,
  DataTable,
  EmptyState,
  FilterBar,
  Select,
  StatusBadge,
} from "@fresh/ui";
import { FolderTree, Plus } from "lucide-react";
import { useMemo } from "react";
import { Link } from "react-router";
import { useCategories, useProducts } from "../../api/catalog";
import { type AdminProductListItem } from "../../api/types/catalog";
import { PageHeader, tableError } from "../../components/page";
import { useDebouncedSearch, useListParams } from "../../lib/list-params";
import { PRODUCT_STATUS, statusOptions } from "../../lib/status";
import { categoryOptions, flattenCategories } from "./category-options";

const FILTERS = ["categoryId", "status"] as const;
const ALL = "__all";

export function ProductsListPage() {
  const list = useListParams();
  const search = useDebouncedSearch(list);
  const products = useProducts(list.query(FILTERS));
  const categories = useCategories(true);

  const categoryName = useMemo(() => {
    const map = new Map(
      flattenCategories(categories.data ?? []).map((f) => [f.category.id, f.path]),
    );
    return (id: string) => map.get(id) ?? "—";
  }, [categories.data]);

  const columns = useMemo<ColumnDef<AdminProductListItem>[]>(
    () => [
      {
        id: "name",
        header: "Product",
        cell: ({ row }) => (
          <div className="flex items-center gap-3">
            {row.original.imageUrl ? (
              <img
                src={row.original.imageUrl}
                alt=""
                className="border-border size-10 rounded-md border object-cover"
              />
            ) : (
              <span aria-hidden="true" className="bg-muted size-10 rounded-md" />
            )}
            <span className="flex flex-col">
              <Link
                to={`/products/${row.original.id}`}
                className="text-accent-text font-medium hover:underline"
              >
                {row.original.name}
              </Link>
              <span className="text-text-muted text-xs">{row.original.slug}</span>
            </span>
          </div>
        ),
      },
      {
        id: "category",
        header: "Category",
        cell: ({ row }) => categoryName(row.original.categoryId),
      },
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => <StatusBadge code={row.original.status} map={PRODUCT_STATUS} />,
      },
      {
        id: "activeVariantCount",
        header: "Active variants",
        meta: { align: "right" },
        cell: ({ row }) => row.original.activeVariantCount,
      },
      {
        id: "publishedAt",
        header: "Published",
        cell: ({ row }) => (row.original.publishedAt ? formatDate(row.original.publishedAt) : "—"),
      },
    ],
    [categoryName],
  );

  const status = list.get("status");
  const categoryId = list.get("categoryId");
  const active = [
    ...(categoryId
      ? [
          {
            id: "categoryId",
            label: `Category: ${categoryName(categoryId)}`,
            onRemove: () => list.set({ categoryId: null }),
          },
        ]
      : []),
    ...(status
      ? [
          {
            id: "status",
            label: `Status: ${PRODUCT_STATUS[status]?.label ?? status}`,
            onRemove: () => list.set({ status: null }),
          },
        ]
      : []),
  ];

  return (
    <>
      <PageHeader
        title="Products"
        description="Catalogue products, their variants, preparations and photos."
        actions={
          <>
            <Button asChild variant="outline" leadingIcon={<FolderTree aria-hidden="true" />}>
              <Link to="/products/categories">Categories</Link>
            </Button>
            <Button asChild leadingIcon={<Plus aria-hidden="true" />}>
              <Link to="/products/new">New product</Link>
            </Button>
          </>
        }
      />
      <div className="flex flex-col gap-4">
        <FilterBar
          search={{
            ...search,
            placeholder: "Search name or local name…",
            label: "Search products",
          }}
          activeFilters={active}
          onClearAll={() => list.clear(["q", ...FILTERS])}
        >
          <div className="w-56">
            <Select
              aria-label="Category"
              placeholder="All categories"
              options={[
                { value: ALL, label: "All categories" },
                ...categoryOptions(categories.data),
              ]}
              value={categoryId ?? ALL}
              onValueChange={(v) => list.set({ categoryId: v === ALL ? null : v })}
            />
          </div>
          <div className="w-44">
            <Select
              aria-label="Status"
              options={[{ value: ALL, label: "Any status" }, ...statusOptions(PRODUCT_STATUS)]}
              value={status ?? ALL}
              onValueChange={(v) => list.set({ status: v === ALL ? null : v })}
            />
          </div>
        </FilterBar>
        <DataTable
          caption="Products"
          columns={columns}
          data={products.data?.items ?? []}
          getRowId={(r) => r.id}
          page={products.data?.page ?? list.page}
          pageSize={products.data?.pageSize ?? list.pageSize}
          totalCount={products.data?.totalCount ?? 0}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
          isLoading={products.isFetching}
          error={tableError(products.error)}
          onRetry={() => void products.refetch()}
          emptyState={
            <EmptyState
              title={
                list.q || categoryId || status
                  ? "No products match these filters"
                  : "No products yet"
              }
              description="Create a product, then add variants, preparations and photos before publishing."
            />
          }
        />
      </div>
    </>
  );
}
