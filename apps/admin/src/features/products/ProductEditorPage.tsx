import {
  Badge,
  Button,
  type ColumnDef,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  StatusBadge,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@fresh/ui";
import { Pencil, Plus } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { useBlocker, useParams } from "react-router";
import {
  type ProductTransition,
  useProduct,
  useProductTransition,
  useUpdateProduct,
} from "../../api/catalog";
import { type AdminProduct, type AdminVariant } from "../../api/types/catalog";
import { PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { PRODUCT_STATUS } from "../../lib/status";
import { bpToPercentInput } from "../../lib/units";
import { ImagesPanel } from "./ImagesPanel";
import { ProductDetailsForm } from "./ProductDetailsForm";
import { VariantDialog } from "./VariantDialog";

export function ProductEditorPage() {
  const { productId = "" } = useParams();
  const product = useProduct(productId);
  return (
    <QueryView query={product} what="product">
      {(data) => <Editor product={data} reload={() => product.refetch()} />}
    </QueryView>
  );
}

const TRANSITIONS: Record<AdminProduct["status"], { action: ProductTransition; label: string }[]> =
  {
    DRAFT: [{ action: "publish", label: "Publish" }],
    ACTIVE: [{ action: "archive", label: "Archive" }],
    ARCHIVED: [{ action: "restore", label: "Restore to draft" }],
  };

function Editor({ product, reload }: { product: AdminProduct; reload: () => unknown }) {
  const update = useUpdateProduct(product.id);
  const transition = useProductTransition(product.id);
  const [dirty, setDirty] = useState(false);
  // A new form after a conflict reload so the fields show the server's values.
  const [formKey, setFormKey] = useState(0);
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty && currentLocation.pathname !== nextLocation.pathname,
  );

  const reloadAfterConflict = useCallback(async () => {
    await reload();
    setFormKey((k) => k + 1);
  }, [reload]);

  const run = (action: ProductTransition, label: string) =>
    transition.mutate(
      { action, version: product.version },
      {
        onSuccess: () => notifySaved(`${label}: done`),
        onError: (error) => reportError(error, { onConflict: reload, title: `${label} refused` }),
      },
    );

  return (
    <>
      <PageHeader
        title={product.name}
        description={`/${product.slug}`}
        backTo="/products"
        backLabel="Products"
        meta={<StatusBadge code={product.status} map={PRODUCT_STATUS} />}
        actions={TRANSITIONS[product.status].map((t) => (
          <Button
            key={t.action}
            variant={t.action === "archive" ? "outline" : "primary"}
            loading={transition.isPending && transition.variables.action === t.action}
            onClick={() => run(t.action, t.label)}
          >
            {t.label}
          </Button>
        ))}
      />
      <Tabs defaultValue="details" className="flex flex-col gap-4">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="variants">Variants ({product.variants.length})</TabsTrigger>
          <TabsTrigger value="images">Photos ({product.images.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="details">
          <Section title="Details">
            <ProductDetailsForm
              key={formKey}
              product={product}
              submitLabel="Save details"
              onDirtyChange={setDirty}
              onConflict={reloadAfterConflict}
              onSubmit={async (body) => {
                const saved = await update.mutateAsync({ body, version: product.version });
                notifySaved("Product saved");
                return saved;
              }}
            />
          </Section>
        </TabsContent>
        <TabsContent value="variants">
          <VariantsPanel product={product} onConflict={reload} />
        </TabsContent>
        <TabsContent value="images">
          <ImagesPanel product={product} onConflict={reload} />
        </TabsContent>
      </Tabs>
      <Dialog
        open={blocker.state === "blocked"}
        onOpenChange={(open) => !open && blocker.reset?.()}
      >
        <DialogContent
          size="sm"
          title="Leave without saving?"
          description="Your changes to the product details are not saved."
          footer={
            <>
              <Button variant="outline" onClick={() => blocker.reset?.()}>
                Stay
              </Button>
              <Button variant="danger" onClick={() => blocker.proceed?.()}>
                Leave
              </Button>
            </>
          }
        />
      </Dialog>
    </>
  );
}

const UNIT_LABEL: Record<AdminVariant["sellUnit"], string> = {
  KG: "per kg",
  PIECE: "per piece",
  PACK: "per pack",
};

function quantity(value: number, variant: AdminVariant) {
  return variant.stockUnit === "GRAM"
    ? `${value} g`
    : `${value} ${variant.sellUnit === "PACK" ? "pack" : "pc"}`;
}

function VariantsPanel({
  product,
  onConflict,
}: {
  product: AdminProduct;
  onConflict: () => unknown;
}) {
  const [editing, setEditing] = useState<AdminVariant | null>(null);
  const [open, setOpen] = useState(false);

  const columns = useMemo<ColumnDef<AdminVariant>[]>(
    () => [
      {
        id: "variant",
        header: "Variant",
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span className="font-medium">{row.original.name}</span>
            <span className="text-text-muted font-mono text-xs">{row.original.sku}</span>
          </span>
        ),
      },
      { id: "unit", header: "Sold", cell: ({ row }) => UNIT_LABEL[row.original.sellUnit] },
      {
        id: "limits",
        header: "Min / max / step",
        cell: ({ row }) => {
          const v = row.original;
          return `${quantity(v.minOrder, v)} / ${quantity(v.maxOrder, v)} / ${quantity(v.orderStep, v)}`;
        },
      },
      {
        id: "preparations",
        header: "Preparations (yield)",
        cell: ({ row }) => (
          <ul className="flex flex-wrap gap-1">
            {row.original.preparations.map((p) => (
              <li key={p.code}>
                <Badge tone={p.isDefault ? "accent" : "neutral"}>
                  {p.name} {bpToPercentInput(p.yieldBp)} %{p.isDefault ? " · default" : ""}
                </Badge>
              </li>
            ))}
          </ul>
        ),
      },
      {
        id: "active",
        header: "Status",
        cell: ({ row }) =>
          row.original.isActive ? <Badge tone="success">Active</Badge> : <Badge>Inactive</Badge>,
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        meta: { align: "right" },
        cell: ({ row }) => (
          <Button
            variant="ghost"
            size="sm"
            leadingIcon={<Pencil aria-hidden="true" />}
            aria-label={`Edit variant ${row.original.sku}`}
            onClick={() => {
              setEditing(row.original);
              setOpen(true);
            }}
          >
            Edit
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <Section
      title="Variants"
      description="Each variant is one sellable size or cut. Prices are set per location in Pricing."
      actions={
        <Button
          leadingIcon={<Plus aria-hidden="true" />}
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          Add variant
        </Button>
      }
    >
      <DataTable
        caption="Variants"
        columns={columns}
        data={product.variants}
        getRowId={(v) => v.id}
        page={1}
        pageSize={Math.max(product.variants.length, 10)}
        totalCount={product.variants.length}
        onPageChange={() => undefined}
        emptyState={
          <EmptyState
            title="No variants yet"
            description="Add at least one active variant with a default preparation before publishing."
          />
        }
      />
      {open && (
        <VariantDialog
          product={product}
          variant={editing}
          open={open}
          onOpenChange={setOpen}
          onConflict={onConflict}
        />
      )}
    </Section>
  );
}
