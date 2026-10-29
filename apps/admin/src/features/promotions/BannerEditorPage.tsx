import { zodResolver } from "@hookform/resolvers/zod";
import { Badge, Button, Combobox, FormField, Select } from "@fresh/ui";
import { useMemo, useState } from "react";
import { Controller, type UseFormReturn, useForm, useWatch } from "react-hook-form";
import { useNavigate, useParams } from "react-router";
import { z } from "zod";
import { useCategories, useProduct, useProducts } from "../../api/catalog";
import { useCityLookup } from "../../api/locations";
import { useBanner, useCreateBanner, useUpdateBanner } from "../../api/promotions";
import { type AdminCategory } from "../../api/types/catalog";
import { type Promotion, type PromotionRequest } from "../../api/types/promotions";
import { SelectField, SwitchField, TextField } from "../../components/form";
import { PageHeader, QueryView, Section } from "../../components/page";
import { useDebouncedValue } from "../../lib/debounce";
import { notifySaved, reportError } from "../../lib/feedback";
import { istLocalToUtcIso, parseWholeNumber, utcIsoToIstLocal } from "../../lib/units";
import { LINK_TYPE_LABELS } from "./shared";

const ALL_CITIES = "__all";

// Lengths follow the server's rules (Promotion.MaxTitleLength etc.); the server still checks everything.
const schema = z
  .object({
    title: z
      .string()
      .trim()
      .min(1, "Enter the banner title.")
      .max(120, "Keep the title under 120 characters."),
    subtitle: z.string().trim().max(200, "Keep the subtitle under 200 characters."),
    imageKey: z.string().trim(),
    badge: z.string().trim().max(20, "Keep the badge under 20 characters."),
    linkType: z.enum(["CATEGORY", "PRODUCT", "COUPON"]),
    linkTarget: z.string().trim().min(1, "Choose what the banner opens."),
    cityId: z.string(),
    startsAt: z.string().min(1, "Choose when the banner starts showing."),
    endsAt: z.string(),
    sortOrder: z
      .string()
      .trim()
      .refine((v) => parseWholeNumber(v) !== null, "Enter a whole number, e.g. 1."),
    isActive: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (v.endsAt && v.endsAt <= v.startsAt)
      ctx.addIssue({ code: "custom", path: ["endsAt"], message: "End must be after the start." });
  });

type Values = z.infer<typeof schema>;

const FIELDS = [
  "title",
  "subtitle",
  "imageKey",
  "badge",
  "linkType",
  "linkTarget",
  "cityId",
  "startsAt",
  "endsAt",
  "sortOrder",
  "isActive",
] as const;

function toValues(p: Promotion | null): Values {
  if (!p)
    return {
      title: "",
      subtitle: "",
      imageKey: "",
      badge: "",
      linkType: "CATEGORY",
      linkTarget: "",
      cityId: ALL_CITIES,
      startsAt: "",
      endsAt: "",
      sortOrder: "1",
      isActive: true,
    };
  return {
    title: p.title,
    subtitle: p.subtitle ?? "",
    imageKey: p.imageKey ?? "",
    badge: p.badge ?? "",
    linkType: p.linkType,
    linkTarget: p.linkTarget,
    cityId: p.cityId ?? ALL_CITIES,
    startsAt: utcIsoToIstLocal(p.startsAt),
    endsAt: p.endsAt ? utcIsoToIstLocal(p.endsAt) : "",
    sortOrder: String(p.sortOrder),
    isActive: p.isActive,
  };
}

/** Every field on every save: the PUT replaces the banner, so a field left out would be cleared. */
function toRequest(v: Values): PromotionRequest {
  return {
    title: v.title,
    subtitle: v.subtitle || null,
    imageKey: v.imageKey || null,
    badge: v.badge || null,
    linkType: v.linkType,
    linkTarget: v.linkType === "COUPON" ? v.linkTarget.toUpperCase() : v.linkTarget,
    cityId: v.cityId === ALL_CITIES ? null : v.cityId,
    startsAt: istLocalToUtcIso(v.startsAt) ?? v.startsAt,
    endsAt: v.endsAt ? (istLocalToUtcIso(v.endsAt) ?? v.endsAt) : null,
    sortOrder: parseWholeNumber(v.sortOrder) ?? 0,
    isActive: v.isActive,
  };
}

export function BannerCreatePage() {
  return <BannerForm banner={null} reload={() => undefined} />;
}

export function BannerEditorPage() {
  const { promotionId = "" } = useParams();
  const query = useBanner(promotionId);
  return (
    <QueryView query={query} what="banner">
      {(banner) => (
        <BannerForm key={banner.version} banner={banner} reload={() => query.refetch()} />
      )}
    </QueryView>
  );
}

function flatten(categories: AdminCategory[], depth = 0): { value: string; label: string }[] {
  return categories.flatMap((c) => [
    { value: c.id, label: `${"— ".repeat(depth)}${c.name}` },
    ...flatten(c.children ?? [], depth + 1),
  ]);
}

function BannerForm({ banner, reload }: { banner: Promotion | null; reload: () => unknown }) {
  const navigate = useNavigate();
  const create = useCreateBanner();
  const update = useUpdateBanner(banner?.id ?? "");
  const cities = useCityLookup();
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: toValues(banner) });
  const linkType = useWatch({ control: form.control, name: "linkType" });

  const cityOptions = [
    { value: ALL_CITIES, label: "All cities" },
    ...(cities.data?.items ?? []).map((c) => ({ value: c.id, label: c.name })),
    ...(banner?.cityId && !cities.data
      ? [{ value: banner.cityId, label: `City ${banner.cityId.slice(-6)}` }]
      : []),
  ];

  const onSubmit = form.handleSubmit((values) => {
    const body = toRequest(values);
    const onError = (error: unknown) =>
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        onConflict: reload,
        title: "Could not save the banner",
      });
    if (!banner) {
      create.mutate(body, {
        onSuccess: (saved) => {
          notifySaved("Banner created");
          void navigate(`/banners/${saved.id}`, { replace: true });
        },
        onError,
      });
    } else {
      update.mutate(
        { body, version: banner.version },
        { onSuccess: () => notifySaved("Banner saved"), onError },
      );
    }
  });

  return (
    <>
      <PageHeader
        backTo="/banners"
        backLabel="Home banners"
        title={banner ? banner.title : "New banner"}
        meta={
          banner && (
            <Badge tone={banner.isActive ? "success" : "neutral"}>
              {banner.isActive ? "Active" : "Off"}
            </Badge>
          )
        }
      />
      <form
        noValidate
        onSubmit={onSubmit}
        aria-label="Banner"
        className="flex max-w-3xl flex-col gap-6"
      >
        <Section title="Banner">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <TextField form={form} name="title" label="Title" required />
            </div>
            <div className="sm:col-span-2">
              <TextField form={form} name="subtitle" label="Subtitle" />
            </div>
            <TextField
              form={form}
              name="imageKey"
              label="Image key"
              hint="The stored picture's key; empty shows the banner without a picture."
            />
            <TextField
              form={form}
              name="badge"
              label="Badge"
              hint="A short tag on the banner, e.g. New. Up to 20 characters."
            />
          </div>
        </Section>

        <Section title="Opens">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Opens a" required error={form.formState.errors.linkType?.message}>
              <Controller
                control={form.control}
                name="linkType"
                render={({ field }) => (
                  <Select
                    options={Object.entries(LINK_TYPE_LABELS).map(([value, label]) => ({
                      value,
                      label,
                    }))}
                    value={field.value}
                    onValueChange={(next) => {
                      field.onChange(next);
                      // A category id is not a product id or a coupon code: choose the target again.
                      form.setValue("linkTarget", "");
                    }}
                  />
                )}
              />
            </FormField>
            <LinkTargetField form={form} linkType={linkType} />
          </div>
        </Section>

        <Section title="Where and when">
          <div className="grid gap-4 sm:grid-cols-2">
            <SelectField form={form} name="cityId" label="City" options={cityOptions} />
            <TextField
              form={form}
              name="sortOrder"
              label="Order"
              inputMode="numeric"
              required
              hint="Lower numbers show first."
            />
            <TextField
              form={form}
              name="startsAt"
              label="Starts (IST)"
              type="datetime-local"
              required
            />
            <TextField
              form={form}
              name="endsAt"
              label="Ends (IST)"
              type="datetime-local"
              hint="Empty means it shows until it is switched off."
            />
            <div className="flex items-end">
              <SwitchField form={form} name="isActive" label="Active" />
            </div>
          </div>
        </Section>

        <div>
          <Button type="submit" loading={create.isPending || update.isPending}>
            {banner ? "Save banner" : "Create banner"}
          </Button>
        </div>
      </form>
    </>
  );
}

/** The link target control for the chosen link type: a category, a product (searched on the server) or a coupon code. */
function LinkTargetField({
  form,
  linkType,
}: {
  form: UseFormReturn<Values>;
  linkType: Values["linkType"];
}) {
  if (linkType === "COUPON") {
    return (
      <TextField
        form={form}
        name="linkTarget"
        label="Coupon code"
        required
        hint="The coupon must exist; the server checks it."
      />
    );
  }
  if (linkType === "PRODUCT") return <ProductTarget form={form} />;
  return <CategoryTarget form={form} />;
}

function CategoryTarget({ form }: { form: UseFormReturn<Values> }) {
  const categories = useCategories(false);
  const options = useMemo(() => flatten(categories.data ?? []), [categories.data]);
  return (
    <SelectField
      form={form}
      name="linkTarget"
      label="Category"
      required
      options={options}
      placeholder={categories.isPending ? "Loading categories…" : "Choose a category"}
    />
  );
}

function ProductTarget({ form }: { form: UseFormReturn<Values> }) {
  const [search, setSearch] = useState("");
  const q = useDebouncedValue(search).trim();
  const products = useProducts({ status: "ACTIVE", pageSize: 20, ...(q ? { q } : {}) });
  const value = useWatch({ control: form.control, name: "linkTarget" });
  const chosen = useProduct(value, Boolean(value));
  return (
    <FormField label="Product" required error={form.formState.errors.linkTarget?.message}>
      <Controller
        control={form.control}
        name="linkTarget"
        render={({ field }) => (
          <Combobox
            options={(products.data?.items ?? []).map((p) => ({ value: p.id, label: p.name }))}
            value={field.value || null}
            onValueChange={(id) => field.onChange(id ?? "")}
            onSearchChange={setSearch}
            loading={products.isFetching}
            {...(chosen.data ? { selectedLabel: chosen.data.name } : {})}
            placeholder="Choose a product"
            searchPlaceholder="Search products"
            emptyText="No active product matches."
          />
        )}
      />
    </FormField>
  );
}
