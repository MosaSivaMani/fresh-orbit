import { isApiError } from "@fresh/core";
import { Button, EmptyState, Skeleton } from "@fresh/ui";
import { ArrowLeft, MapPin } from "lucide-react";
import { type ReactNode } from "react";
import { Link } from "react-router";
import { useCan } from "../app/services";
import { type PermissionRequirement } from "../auth/permissions";
import { IllustratedState, LoadError } from "./states";

export function PageHeader({
  title,
  description,
  actions,
  backTo,
  backLabel = "Back",
  meta,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  backTo?: string;
  backLabel?: string;
  /** Badges next to the title (status). */
  meta?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-col gap-3">
      {backTo && (
        <Link
          to={backTo}
          className="text-text-muted hover:text-text inline-flex w-fit items-center gap-1 text-sm"
        >
          <ArrowLeft aria-hidden="true" className="size-4" />
          {backLabel}
        </Link>
      )}
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display text-2xl leading-8 font-semibold">{title}</h1>
            {meta}
          </div>
          {description && <p className="text-text-muted text-sm">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}

export function NoAccess({ title = "You do not have access to this page" }: { title?: string }) {
  return (
    <IllustratedState
      slot="ADMIN_NO_ACCESS"
      headingLevel={1}
      title={title}
      description="Ask an administrator to give your role the permission it needs."
      action={
        <Button asChild variant="outline" size="sm">
          <Link to="/">Go to dashboard</Link>
        </Button>
      }
    />
  );
}

export function NotFound({ what = "page" }: { what?: string }) {
  return (
    <IllustratedState
      slot="NOT_FOUND"
      headingLevel={1}
      title={`This ${what} was not found`}
      description="It may have been removed, or it belongs to a location you cannot see."
      action={
        <Button asChild variant="outline" size="sm">
          <Link to="/">Go to dashboard</Link>
        </Button>
      }
    />
  );
}

/** Renders the route only for staff holding the permission(s); otherwise the "no access" page. */
export function RequirePermission({
  perm,
  children,
}: {
  perm: PermissionRequirement | undefined;
  children: ReactNode;
}) {
  const can = useCan();
  return can(perm) ? children : <NoAccess />;
}

/** Shows children only when permitted (buttons, menu items). The API still enforces. */
export function Can({ perm, children }: { perm: PermissionRequirement; children: ReactNode }) {
  const can = useCan();
  return can(perm) ? children : null;
}

/** Loading / error / not-found / forbidden states for a single-resource query. */
export function QueryView<T>({
  query,
  what = "record",
  children,
  loading,
}: {
  query: {
    data: T | undefined;
    error: unknown;
    isPending: boolean;
    refetch: () => unknown;
  };
  what?: string;
  loading?: ReactNode;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) {
    return (
      loading ?? (
        <div aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      )
    );
  }
  if (query.error) {
    if (isApiError(query.error) && query.error.status === 403) return <NoAccess />;
    if (isApiError(query.error) && query.error.status === 404) return <NotFound what={what} />;
    return (
      <LoadError
        title={`Could not load this ${what}`}
        traceId={isApiError(query.error) ? query.error.traceId : undefined}
        onRetry={() => void query.refetch()}
      />
    );
  }
  if (query.data === undefined) return null;
  return children(query.data);
}

/** DataTable `error` prop from a query error. */
export function tableError(error: unknown): { traceId?: string; message?: string } | null {
  if (!error) return null;
  if (isApiError(error)) {
    if (error.status === 403) return { message: "You do not have access to this list." };
    return error.traceId ? { traceId: error.traceId } : {};
  }
  return {};
}

/** Shown when a screen needs one fulfillment location and the switcher is on "All locations". */
export function LocationRequired({ action = "see this page" }: { action?: string }) {
  return (
    <EmptyState
      icon={<MapPin aria-hidden="true" />}
      title="Choose a location"
      description={`Pick a fulfillment location in the top bar to ${action}.`}
    />
  );
}

/** Two-column label/value list for detail pages. */
export function DetailList({
  items,
  columns = 2,
}: {
  items: readonly { label: string; value: ReactNode }[];
  columns?: 1 | 2 | 3;
}) {
  const grid = columns === 3 ? "sm:grid-cols-3" : columns === 2 ? "sm:grid-cols-2" : "";
  return (
    <dl className={`grid grid-cols-1 gap-x-6 gap-y-3 ${grid}`}>
      {items.map((item) => (
        <div key={item.label} className="flex min-w-0 flex-col gap-0.5">
          <dt className="text-text-muted text-xs font-medium tracking-wide uppercase">
            {item.label}
          </dt>
          <dd className="text-sm break-words">{item.value ?? "—"}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Small "Section" wrapper used inside pages. */
export function Section({
  title,
  actions,
  children,
  description,
}: {
  title: ReactNode;
  actions?: ReactNode;
  description?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="border-border bg-surface flex flex-col gap-4 rounded-lg border p-5 shadow-xs">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-0.5">
          <h2 className="font-display text-lg leading-7 font-semibold">{title}</h2>
          {description && <p className="text-text-muted text-sm">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </section>
  );
}
