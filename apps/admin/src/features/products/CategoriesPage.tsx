import { zodResolver } from "@hookform/resolvers/zod";
import { Badge, Button, type ColumnDef, DataTable, Dialog, DialogContent } from "@fresh/ui";
import { Pencil, Plus } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useCategories, useCreateCategory, useUpdateCategory } from "../../api/catalog";
import { type AdminCategory, type UpdateCategoryRequest } from "../../api/types/catalog";
import { SelectField, SwitchField, TextAreaField, TextField } from "../../components/form";
import { PageHeader, tableError } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { type FlatCategory, flattenCategories } from "./category-options";
import { ListEmpty } from "../../components/states";

const NO_PARENT = "__root";

const schema = z.object({
  parentId: z.string(),
  name: z.string().trim().min(1, "Enter the category name."),
  slug: z
    .string()
    .trim()
    .min(1, "Enter a URL slug.")
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Use lowercase letters, digits and single hyphens."),
  description: z.string(),
  sortOrder: z.string().regex(/^\d+$/, "Enter a whole number."),
  isActive: z.boolean(),
});

type CategoryValues = z.infer<typeof schema>;

const FIELDS = ["parentId", "name", "slug", "description", "sortOrder", "isActive"] as const;

function toValues(category: AdminCategory | null): CategoryValues {
  return {
    parentId: category?.parentId ?? NO_PARENT,
    name: category?.name ?? "",
    slug: category?.slug ?? "",
    description: category?.description ?? "",
    sortOrder: String(category?.sortOrder ?? 0),
    isActive: category?.isActive ?? true,
  };
}

function updateBody(
  category: AdminCategory,
  patch: Partial<UpdateCategoryRequest> = {},
): UpdateCategoryRequest {
  return {
    parentId: category.parentId,
    slug: category.slug,
    name: category.name,
    description: category.description,
    sortOrder: category.sortOrder,
    imageKey: category.imageKey,
    isActive: category.isActive,
    ...patch,
  };
}

export function CategoriesPage() {
  const categories = useCategories(true);
  const update = useUpdateCategory();
  const [dialog, setDialog] = useState<{ category: AdminCategory | null } | null>(null);
  const rows = useMemo(() => flattenCategories(categories.data ?? []), [categories.data]);

  const columns = useMemo<ColumnDef<FlatCategory>[]>(
    () => [
      {
        id: "name",
        header: "Category",
        cell: ({ row }) => (
          <span
            className="flex flex-col"
            style={{ paddingInlineStart: `${row.original.depth * 1.5}rem` }}
          >
            <span className="font-medium">{row.original.category.name}</span>
            <span className="text-text-muted text-xs">{row.original.category.slug}</span>
          </span>
        ),
      },
      {
        id: "sortOrder",
        header: "Order",
        meta: { align: "right" },
        cell: ({ row }) => row.original.category.sortOrder,
      },
      {
        id: "isActive",
        header: "Status",
        cell: ({ row }) =>
          row.original.category.isActive ? (
            <Badge tone="success">Active</Badge>
          ) : (
            <Badge>Inactive</Badge>
          ),
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        meta: { align: "right" },
        cell: ({ row }) => {
          const c = row.original.category;
          return (
            <span className="flex justify-end gap-1">
              <Button
                variant="ghost"
                size="sm"
                aria-label={`${c.isActive ? "Deactivate" : "Activate"} ${c.name}`}
                onClick={() =>
                  update.mutate(
                    {
                      id: c.id,
                      body: updateBody(c, { isActive: !c.isActive }),
                      version: c.version,
                    },
                    {
                      onSuccess: () =>
                        notifySaved(c.isActive ? "Category deactivated" : "Category activated"),
                      onError: (error) =>
                        reportError(error, {
                          onConflict: () => categories.refetch(),
                          title: "Category not changed",
                        }),
                    },
                  )
                }
              >
                {c.isActive ? "Deactivate" : "Activate"}
              </Button>
              <Button
                variant="ghost"
                size="sm"
                leadingIcon={<Pencil aria-hidden="true" />}
                aria-label={`Edit ${c.name}`}
                onClick={() => setDialog({ category: c })}
              >
                Edit
              </Button>
            </span>
          );
        },
      },
    ],
    [update, categories],
  );

  return (
    <>
      <PageHeader
        title="Categories"
        description="The category tree on the website and app. Inactive categories are hidden from customers."
        backTo="/products"
        backLabel="Products"
        actions={
          <Button
            leadingIcon={<Plus aria-hidden="true" />}
            onClick={() => setDialog({ category: null })}
          >
            New category
          </Button>
        }
      />
      <DataTable
        caption="Categories"
        columns={columns}
        data={rows}
        getRowId={(r) => r.category.id}
        page={1}
        pageSize={Math.max(rows.length, 10)}
        totalCount={rows.length}
        onPageChange={() => undefined}
        isLoading={categories.isPending}
        error={tableError(categories.error)}
        onRetry={() => void categories.refetch()}
        emptyState={
          <ListEmpty
            title="No categories yet"
            description="Create the first category, e.g. Sea fish."
          />
        }
      />
      {dialog && (
        <CategoryDialog
          category={dialog.category}
          all={rows}
          onClose={() => setDialog(null)}
          onConflict={() => categories.refetch()}
        />
      )}
    </>
  );
}

function CategoryDialog({
  category,
  all,
  onClose,
  onConflict,
}: {
  category: AdminCategory | null;
  all: FlatCategory[];
  onClose: () => void;
  onConflict: () => unknown;
}) {
  const create = useCreateCategory();
  const update = useUpdateCategory();
  const form = useForm<CategoryValues>({
    resolver: zodResolver(schema),
    defaultValues: toValues(category),
  });

  // A category cannot sit under itself or its own descendants.
  const excluded = new Set<string>();
  if (category) flattenCategories([category]).forEach((f) => excluded.add(f.category.id));
  const parentOptions = [
    { value: NO_PARENT, label: "None (top level)" },
    ...all
      .filter((f) => !excluded.has(f.category.id))
      .map((f) => ({ value: f.category.id, label: f.path })),
  ];

  const submit = form.handleSubmit(async (values) => {
    const base = {
      parentId: values.parentId === NO_PARENT ? null : values.parentId,
      slug: values.slug.trim(),
      name: values.name.trim(),
      description: values.description.trim() || null,
      sortOrder: Number(values.sortOrder),
    };
    try {
      if (category) {
        await update.mutateAsync({
          id: category.id,
          body: { ...base, imageKey: category.imageKey, isActive: values.isActive },
          version: category.version,
        });
      } else {
        await create.mutateAsync(base);
      }
      notifySaved(category ? "Category saved" : "Category created");
      onClose();
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        title: "Category not saved",
        onConflict: () => {
          onClose();
          return onConflict();
        },
      });
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent title={category ? `Edit ${category.name}` : "New category"}>
        <form noValidate onSubmit={submit} className="flex flex-col gap-4">
          <TextField form={form} name="name" label="Name" required />
          <TextField
            form={form}
            name="slug"
            label="URL slug"
            required
            hint="Lowercase, e.g. sea-fish."
          />
          <SelectField
            form={form}
            name="parentId"
            label="Parent category"
            options={parentOptions}
          />
          <TextAreaField form={form} name="description" label="Description" rows={2} />
          <TextField form={form} name="sortOrder" label="Sort order" inputMode="numeric" required />
          {category && (
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive categories are hidden."
            />
          )}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={form.formState.isSubmitting}>
              {category ? "Save category" : "Create category"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
