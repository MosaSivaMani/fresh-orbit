import { zodResolver } from "@hookform/resolvers/zod";
import { formatDateTime, formatRupees } from "@fresh/core";
import {
  Badge,
  Button,
  type ColumnDef,
  Combobox,
  DataTable,
  EmptyState,
  FormField,
  StatusBadge,
} from "@fresh/ui";
import { Pause, Play, X } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useNavigate, useParams } from "react-router";
import { z } from "zod";
import { useCategories } from "../../api/catalog";
import { useCityLookup } from "../../api/locations";
import {
  useCoupon,
  useCouponRedemptions,
  useCouponTransition,
  useCreateCoupon,
  useUpdateCoupon,
} from "../../api/promotions";
import { type AdminCategory } from "../../api/types/catalog";
import {
  type Coupon,
  type CouponRedemption,
  type CouponRequest,
  type CouponScope,
} from "../../api/types/promotions";
import { useCan } from "../../app/services";
import { SelectField, SwitchField, TextAreaField, TextField } from "../../components/form";
import { PageHeader, QueryView, Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";
import { COUPON_STATUS } from "../../lib/status";
import {
  bpToPercentInput,
  istLocalToUtcIso,
  paiseToRupeesInput,
  parsePercentToBp,
  parseRupeesToPaise,
  parseWholeNumber,
  utcIsoToIstLocal,
} from "../../lib/units";
import { discountLabel } from "./shared";

const ALL_CITIES = "__all";

const optionalCount = z
  .string()
  .trim()
  .refine(
    (v) => v === "" || (parseWholeNumber(v) ?? 0) >= 1,
    "Enter a whole number of 1 or more, or leave empty.",
  );

const schema = z
  .object({
    code: z
      .string()
      .trim()
      .toUpperCase()
      .regex(/^[A-Z0-9]{3,20}$/, "Use 3–20 capital letters or digits."),
    description: z
      .string()
      .trim()
      .min(1, "Describe the offer for customers.")
      .max(200, "Keep it under 200 characters."),
    discountType: z.enum(["PERCENT", "FLAT", "FREE_DELIVERY"]),
    discountValue: z.string().trim(),
    maxDiscount: z.string().trim(),
    minOrder: z.string().trim(),
    cityId: z.string(),
    firstOrderOnly: z.boolean(),
    perCustomerLimit: optionalCount,
    totalLimit: optionalCount,
    validFrom: z.string().min(1, "Choose when the coupon starts."),
    validTo: z.string().min(1, "Choose when the coupon ends."),
  })
  .superRefine((v, ctx) => {
    if (v.discountType === "PERCENT") {
      const bp = parsePercentToBp(v.discountValue);
      if (bp === null || bp < 1 || bp > 10000)
        ctx.addIssue({
          code: "custom",
          path: ["discountValue"],
          message: "Enter a percentage between 0.01 and 100.",
        });
      const cap = parseRupeesToPaise(v.maxDiscount);
      if (cap === null || cap <= 0)
        ctx.addIssue({
          code: "custom",
          path: ["maxDiscount"],
          message: "Enter the largest discount in rupees.",
        });
    }
    if (v.discountType === "FLAT") {
      const paise = parseRupeesToPaise(v.discountValue);
      if (paise === null || paise <= 0)
        ctx.addIssue({
          code: "custom",
          path: ["discountValue"],
          message: "Enter the discount in rupees.",
        });
    }
    if (v.minOrder !== "" && parseRupeesToPaise(v.minOrder) === null)
      ctx.addIssue({
        code: "custom",
        path: ["minOrder"],
        message: "Enter the minimum order in rupees, or leave empty.",
      });
    if (v.firstOrderOnly && v.perCustomerLimit !== "1")
      ctx.addIssue({
        code: "custom",
        path: ["perCustomerLimit"],
        message: "First-order coupons must allow 1 use per customer.",
      });
    if (v.validFrom && v.validTo && v.validTo <= v.validFrom)
      ctx.addIssue({ code: "custom", path: ["validTo"], message: "End must be after the start." });
  });

type Values = z.infer<typeof schema>;

const FIELDS = [
  "code",
  "description",
  "discountType",
  "discountValue",
  "maxDiscount",
  "minOrder",
  "cityId",
  "firstOrderOnly",
  "perCustomerLimit",
  "totalLimit",
  "validFrom",
  "validTo",
] as const;
const ALIASES = { maxDiscountPaise: "maxDiscount", minOrderPaise: "minOrder" };

function toValues(c: Coupon | null): Values {
  if (!c)
    return {
      code: "",
      description: "",
      discountType: "PERCENT",
      discountValue: "",
      maxDiscount: "",
      minOrder: "",
      cityId: ALL_CITIES,
      firstOrderOnly: false,
      perCustomerLimit: "1",
      totalLimit: "",
      validFrom: "",
      validTo: "",
    };
  return {
    code: c.code,
    description: c.description ?? "",
    discountType: c.discountType,
    discountValue:
      c.discountType === "PERCENT"
        ? bpToPercentInput(c.discountValue)
        : c.discountType === "FLAT"
          ? paiseToRupeesInput(c.discountValue)
          : "",
    maxDiscount: paiseToRupeesInput(c.maxDiscountPaise),
    minOrder: c.minOrderPaise > 0 ? paiseToRupeesInput(c.minOrderPaise) : "",
    cityId: c.cityId ?? ALL_CITIES,
    firstOrderOnly: c.firstOrderOnly,
    perCustomerLimit: c.perCustomerLimit != null ? String(c.perCustomerLimit) : "",
    totalLimit: c.totalLimit != null ? String(c.totalLimit) : "",
    validFrom: utcIsoToIstLocal(c.validFrom),
    validTo: utcIsoToIstLocal(c.validTo),
  };
}

function toRequest(v: Values, scopes: CouponScope[], isListed: boolean): CouponRequest {
  const value =
    v.discountType === "PERCENT"
      ? (parsePercentToBp(v.discountValue) ?? 0)
      : v.discountType === "FLAT"
        ? (parseRupeesToPaise(v.discountValue) ?? 0)
        : 0;
  return {
    code: v.code,
    description: v.description,
    discountType: v.discountType,
    discountValue: value,
    maxDiscountPaise: v.discountType === "PERCENT" ? parseRupeesToPaise(v.maxDiscount) : null,
    minOrderPaise: v.minOrder ? (parseRupeesToPaise(v.minOrder) ?? 0) : 0,
    cityId: v.cityId === ALL_CITIES ? null : v.cityId,
    firstOrderOnly: v.firstOrderOnly,
    perCustomerLimit: v.perCustomerLimit ? parseWholeNumber(v.perCustomerLimit) : null,
    totalLimit: v.totalLimit ? parseWholeNumber(v.totalLimit) : null,
    validFrom: istLocalToUtcIso(v.validFrom) ?? v.validFrom,
    validTo: istLocalToUtcIso(v.validTo) ?? v.validTo,
    scopes,
    isListed,
  };
}

export function CouponCreatePage() {
  return <CouponForm coupon={null} reload={() => undefined} />;
}

export function CouponEditorPage() {
  const { couponId = "" } = useParams();
  const query = useCoupon(couponId);
  return (
    <QueryView query={query} what="coupon">
      {(coupon) => (
        <CouponForm key={coupon.version} coupon={coupon} reload={() => query.refetch()} />
      )}
    </QueryView>
  );
}

function flatten(categories: AdminCategory[], depth = 0): { id: string; label: string }[] {
  return categories.flatMap((c) => [
    { id: c.id, label: `${"— ".repeat(depth)}${c.name}` },
    ...flatten(c.children ?? [], depth + 1),
  ]);
}

function CouponForm({ coupon, reload }: { coupon: Coupon | null; reload: () => unknown }) {
  const can = useCan();
  const navigate = useNavigate();
  const create = useCreateCoupon();
  const update = useUpdateCoupon(coupon?.id ?? "");
  const transition = useCouponTransition(coupon?.id ?? "");
  const cities = useCityLookup();
  const categories = useCategories(false, can("catalog.manage"));
  const [scopes, setScopes] = useState<CouponScope[]>(coupon?.scopes ?? []);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: toValues(coupon) });
  const discountType = useWatch({ control: form.control, name: "discountType" });
  const expired = coupon?.status === "EXPIRED";
  const codeLocked = (coupon?.usedCount ?? 0) > 0;

  const categoryOptions = useMemo(() => flatten(categories.data ?? []), [categories.data]);
  const categoryName = (id: string) =>
    categoryOptions.find((o) => o.id === id)?.label.replace(/^(— )+/, "") ?? id;

  const cityOptions = [
    { value: ALL_CITIES, label: "All cities" },
    ...(cities.data?.items ?? []).map((c) => ({ value: c.id, label: c.name })),
    ...(coupon?.cityId && !cities.data
      ? [{ value: coupon.cityId, label: `City ${coupon.cityId.slice(-6)}` }]
      : []),
  ];

  const onSubmit = form.handleSubmit((values) => {
    // No control for `isListed` yet: an edit keeps the saved value, a new coupon is not listed.
    const body = toRequest(values, scopes, coupon?.isListed ?? false);
    const onError = (error: unknown) =>
      reportError(error, {
        setError: form.setError,
        fields: FIELDS,
        aliases: ALIASES,
        onConflict: reload,
        title: "Could not save the coupon",
      });
    if (!coupon) {
      create.mutate(body, {
        onSuccess: (saved) => {
          notifySaved(`Coupon ${saved.code} created`);
          void navigate(`/promotions/${saved.id}`, { replace: true });
        },
        onError,
      });
    } else {
      update.mutate(
        { body, version: coupon.version },
        { onSuccess: () => notifySaved("Coupon saved"), onError },
      );
    }
  });

  return (
    <>
      <PageHeader
        backTo="/promotions"
        backLabel="Coupons"
        title={coupon ? coupon.code : "New coupon"}
        meta={coupon && <StatusBadge code={coupon.status} map={COUPON_STATUS} />}
        description={
          coupon
            ? `${discountLabel(coupon)} · used ${coupon.usedCount}${coupon.totalLimit != null ? ` of ${coupon.totalLimit}` : ""}`
            : undefined
        }
        actions={
          coupon &&
          !expired && (
            <Button
              variant="outline"
              leadingIcon={
                coupon.status === "ACTIVE" ? (
                  <Pause aria-hidden="true" />
                ) : (
                  <Play aria-hidden="true" />
                )
              }
              loading={transition.isPending}
              onClick={() =>
                transition.mutate(
                  {
                    action: coupon.status === "ACTIVE" ? "pause" : "resume",
                    version: coupon.version,
                  },
                  {
                    onSuccess: () =>
                      notifySaved(coupon.status === "ACTIVE" ? "Coupon paused" : "Coupon resumed"),
                    onError: (error) => reportError(error, { onConflict: reload }),
                  },
                )
              }
            >
              {coupon.status === "ACTIVE" ? "Pause" : "Resume"}
            </Button>
          )
        }
      />
      <div className="flex flex-col gap-6">
        <form
          noValidate
          onSubmit={onSubmit}
          aria-label="Coupon"
          className="flex max-w-3xl flex-col gap-6"
        >
          <Section title="Offer">
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                form={form}
                name="code"
                label="Code"
                required
                disabled={codeLocked || expired}
                hint={
                  codeLocked
                    ? "The code cannot change after it has been used."
                    : "3–20 capital letters or digits."
                }
              />
              <SelectField
                form={form}
                name="discountType"
                label="Discount type"
                disabled={expired}
                options={[
                  { value: "PERCENT", label: "Percentage" },
                  { value: "FLAT", label: "Flat amount" },
                  { value: "FREE_DELIVERY", label: "Free delivery" },
                ]}
              />
              <div className="sm:col-span-2">
                <TextAreaField
                  form={form}
                  name="description"
                  label="Description"
                  required
                  disabled={expired}
                />
              </div>
              {discountType !== "FREE_DELIVERY" && (
                <TextField
                  form={form}
                  name="discountValue"
                  label={discountType === "PERCENT" ? "Discount (%)" : "Discount (₹)"}
                  inputMode="decimal"
                  required
                  disabled={expired}
                />
              )}
              {discountType === "PERCENT" && (
                <TextField
                  form={form}
                  name="maxDiscount"
                  label="Largest discount (₹)"
                  inputMode="decimal"
                  required
                  disabled={expired}
                />
              )}
              <TextField
                form={form}
                name="minOrder"
                label="Minimum order (₹)"
                inputMode="decimal"
                hint="Of the eligible items; empty means no minimum."
                disabled={expired}
              />
            </div>
          </Section>

          <Section title="Who and when">
            <div className="grid gap-4 sm:grid-cols-2">
              <SelectField
                form={form}
                name="cityId"
                label="City"
                options={cityOptions}
                disabled={expired}
              />
              <div className="flex items-end">
                <SwitchField
                  form={form}
                  name="firstOrderOnly"
                  label="First order only"
                  disabled={expired}
                />
              </div>
              <TextField
                form={form}
                name="perCustomerLimit"
                label="Uses per customer"
                inputMode="numeric"
                hint="Empty means unlimited."
                disabled={expired}
              />
              <TextField
                form={form}
                name="totalLimit"
                label="Total uses"
                inputMode="numeric"
                hint="Empty means unlimited."
                disabled={expired}
              />
              <TextField
                form={form}
                name="validFrom"
                label="Starts (IST)"
                type="datetime-local"
                required
                disabled={expired}
              />
              <TextField
                form={form}
                name="validTo"
                label="Ends (IST)"
                type="datetime-local"
                required
                disabled={expired}
              />
            </div>
          </Section>

          <Section title="Eligible items">
            <p className="text-text-muted mb-3 text-sm">
              No categories or products means every item is eligible. A category includes its
              sub-categories.
            </p>
            <ul className="mb-3 flex flex-wrap gap-2" aria-label="Eligible categories and products">
              {scopes.map((s) => (
                <li key={`${s.scopeType}-${s.scopeId}`}>
                  <Badge tone="neutral" className="gap-1">
                    {s.scopeType === "CATEGORY"
                      ? categoryName(s.scopeId)
                      : `Product ${s.scopeId.slice(-6)}`}
                    {!expired && (
                      <button
                        type="button"
                        aria-label={`Remove ${s.scopeType === "CATEGORY" ? categoryName(s.scopeId) : "product"}`}
                        onClick={() => setScopes(scopes.filter((x) => x !== s))}
                      >
                        <X aria-hidden="true" className="size-3" />
                      </button>
                    )}
                  </Badge>
                </li>
              ))}
              {scopes.length === 0 && <li className="text-sm">All items</li>}
            </ul>
            {can("catalog.manage") && !expired && (
              <FormField label="Add a category">
                <Combobox
                  options={categoryOptions
                    .filter(
                      (o) => !scopes.some((s) => s.scopeType === "CATEGORY" && s.scopeId === o.id),
                    )
                    .map((o) => ({ value: o.id, label: o.label }))}
                  value={null}
                  placeholder="Choose a category"
                  searchPlaceholder="Search categories"
                  onValueChange={(id) =>
                    id && setScopes([...scopes, { scopeType: "CATEGORY", scopeId: id }])
                  }
                />
              </FormField>
            )}
          </Section>

          {!expired && (
            <div>
              <Button type="submit" loading={create.isPending || update.isPending}>
                {coupon ? "Save coupon" : "Create coupon"}
              </Button>
            </div>
          )}
        </form>

        {coupon && <Redemptions couponId={coupon.id} />}
      </div>
    </>
  );
}

function Redemptions({ couponId }: { couponId: string }) {
  const can = useCan();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const redemptions = useCouponRedemptions(couponId, { page, pageSize });
  const columns = useMemo<ColumnDef<CouponRedemption>[]>(
    () => [
      {
        id: "order",
        header: "Order",
        enableSorting: false,
        cell: ({ row }) =>
          can("orders.view") ? (
            <Link
              to={`/orders/${row.original.orderId}`}
              className="text-accent-text hover:underline"
            >
              {row.original.orderId.slice(-8)}
            </Link>
          ) : (
            row.original.orderId.slice(-8)
          ),
      },
      {
        id: "discount",
        header: "Discount",
        enableSorting: false,
        meta: { align: "right" },
        cell: ({ row }) => formatRupees(row.original.discountPaise),
      },
      {
        id: "status",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <Badge tone={row.original.status === "APPLIED" ? "success" : "neutral"}>
            {row.original.status === "APPLIED" ? "Applied" : "Reversed"}
          </Badge>
        ),
      },
      {
        id: "redeemedAt",
        header: "When",
        enableSorting: false,
        cell: ({ row }) => formatDateTime(row.original.redeemedAt),
      },
    ],
    [can],
  );
  return (
    <Section title="Redemptions">
      <DataTable
        caption="Redemptions"
        columns={columns}
        data={redemptions.data?.items ?? []}
        getRowId={(r) => r.id}
        page={page}
        pageSize={pageSize}
        totalCount={redemptions.data?.totalCount ?? 0}
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPage(1);
        }}
        isLoading={redemptions.isFetching}
        error={redemptions.error ? { message: "Could not load redemptions." } : null}
        onRetry={() => void redemptions.refetch()}
        emptyState={<EmptyState title="Not used yet" />}
      />
    </Section>
  );
}
