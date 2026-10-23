import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@fresh/ui";
import { type ReactNode, useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useCategories } from "../../api/catalog";
import { type AdminProduct, type ProductRequest } from "../../api/types/catalog";
import { SelectField, TextAreaField, TextField } from "../../components/form";
import { reportError } from "../../lib/feedback";
import { categoryOptions } from "./category-options";

const schema = z.object({
  categoryId: z.string().min(1, "Choose a category."),
  name: z.string().trim().min(1, "Enter the product name.").max(200, "Use at most 200 characters."),
  slug: z
    .string()
    .trim()
    .min(1, "Enter a URL slug.")
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Use lowercase letters, digits and single hyphens."),
  localNames: z.string(),
  speciesScientificName: z.string(),
  description: z.string(),
  sourceType: z.enum(["WILD", "FARMED"]),
  sourceRegion: z.string(),
  storageInstructions: z.string(),
  handlingNotes: z.string(),
  hsnCode: z.string(),
  taxCategoryCode: z.string(),
  featuredRank: z.string().regex(/^\d*$/, "Enter a whole number or leave it empty."),
});

export type ProductFormValues = z.infer<typeof schema>;

/** Field names shared by the form and the API's `errors` keys. */
const FIELDS = [
  "categoryId",
  "name",
  "slug",
  "localNames",
  "speciesScientificName",
  "description",
  "sourceType",
  "sourceRegion",
  "storageInstructions",
  "handlingNotes",
  "hsnCode",
  "taxCategoryCode",
  "featuredRank",
] as const;

function toFormValues(product: AdminProduct | null): ProductFormValues {
  return {
    categoryId: product?.categoryId ?? "",
    name: product?.name ?? "",
    slug: product?.slug ?? "",
    localNames: product?.localNames.join(", ") ?? "",
    speciesScientificName: product?.speciesScientificName ?? "",
    description: product?.description ?? "",
    sourceType: product?.sourceType ?? "WILD",
    sourceRegion: product?.sourceRegion ?? "",
    storageInstructions: product?.storageInstructions ?? "",
    handlingNotes: product?.handlingNotes ?? "",
    hsnCode: product?.hsnCode ?? "",
    taxCategoryCode: product?.taxCategoryCode ?? "",
    featuredRank: product?.featuredRank == null ? "" : String(product.featuredRank),
  };
}

const orNull = (s: string) => (s.trim() === "" ? null : s.trim());

function toRequest(values: ProductFormValues): ProductRequest {
  return {
    categoryId: values.categoryId,
    slug: values.slug.trim(),
    name: values.name.trim(),
    localNames: values.localNames
      .split(",")
      .map((n) => n.trim())
      .filter((n) => n.length > 0),
    speciesScientificName: orNull(values.speciesScientificName),
    description: orNull(values.description),
    sourceType: values.sourceType,
    sourceRegion: orNull(values.sourceRegion),
    storageInstructions: orNull(values.storageInstructions),
    handlingNotes: orNull(values.handlingNotes),
    hsnCode: orNull(values.hsnCode),
    taxCategoryCode: orNull(values.taxCategoryCode),
    featuredRank: values.featuredRank === "" ? null : Number(values.featuredRank),
  };
}

/** Product fields (ProductRequest). Server field errors land on the matching field. */
export function ProductDetailsForm({
  product,
  submitLabel,
  onSubmit,
  onConflict,
  onDirtyChange,
  extraActions,
}: {
  product: AdminProduct | null;
  submitLabel: string;
  onSubmit: (body: ProductRequest) => Promise<AdminProduct>;
  onConflict?: () => unknown;
  /** Reports unsaved edits to the parent (unsaved-changes guard). */
  onDirtyChange?: (dirty: boolean) => void;
  extraActions?: ReactNode;
}) {
  const categories = useCategories(true);
  const form = useForm<ProductFormValues>({
    resolver: zodResolver(schema),
    defaultValues: toFormValues(product),
  });

  const dirty = form.formState.isDirty;
  useEffect(() => {
    onDirtyChange?.(dirty);
  }, [dirty, onDirtyChange]);

  const submit = form.handleSubmit(async (values) => {
    try {
      const saved = await onSubmit(toRequest(values));
      form.reset(toFormValues(saved));
    } catch (error) {
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        title: "Product not saved",
        ...(onConflict ? { onConflict } : {}),
      });
    }
  });

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-5">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <TextField form={form} name="name" label="Name" required />
        <TextField
          form={form}
          name="slug"
          label="URL slug"
          required
          hint="Lowercase, e.g. seer-fish-vanjaram."
        />
        <SelectField
          form={form}
          name="categoryId"
          label="Category"
          required
          placeholder="Choose a category"
          options={categoryOptions(categories.data)}
        />
        <SelectField
          form={form}
          name="sourceType"
          label="Source"
          required
          options={[
            { value: "WILD", label: "Wild caught" },
            { value: "FARMED", label: "Farmed" },
          ]}
        />
        <TextField
          form={form}
          name="localNames"
          label="Local names"
          hint="Comma-separated, e.g. Vanjaram, Neymeen, Surmai. Customers can search by these."
          className="md:col-span-2"
        />
        <TextField form={form} name="speciesScientificName" label="Scientific name" />
        <TextField form={form} name="sourceRegion" label="Source region" />
        <TextAreaField
          form={form}
          name="description"
          label="Description"
          className="md:col-span-2"
        />
        <TextAreaField
          form={form}
          name="storageInstructions"
          label="Storage instructions"
          rows={2}
        />
        <TextAreaField form={form} name="handlingNotes" label="Handling notes" rows={2} />
        <TextField form={form} name="hsnCode" label="HSN code" />
        <TextField
          form={form}
          name="taxCategoryCode"
          label="Tax category code"
          hint="Must match a tax category in Pricing → Tax rules."
        />
        <TextField
          form={form}
          name="featuredRank"
          label="Featured rank"
          inputMode="numeric"
          hint="Lower numbers appear first on the home page. Leave empty to not feature."
        />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" loading={form.formState.isSubmitting}>
          {submitLabel}
        </Button>
        {extraActions}
      </div>
    </form>
  );
}
