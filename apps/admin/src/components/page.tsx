import { isApiError } from "@fresh/core";
import {
  Button,
  EmptyState,
  PageHeader as UiPageHeader,
  type PageHeaderProps,
  Skeleton,
  backLinkClass,
} from "@fresh/ui";
import { ArrowLeft, MapPin } from "lucide-react";
import { type ReactNode } from "react";
import { Link } from "react-router";
import { useCan } from "../app/services";
import { type PermissionRequirement } from "../auth/permissions";
import { IllustratedState, LoadError } from "./states";

/** The shared page header (`@fresh/ui`) with a router back link. */
export function PageHeader({
  backTo,
  backLabel = "Back",
  ...props
}: Omit<PageHeaderProps, "back"> & { backTo?: string; backLabel?: string }) {
  return (
    <UiPageHeader
      {...props}
      back={
        backTo ? (
          <Link to={backTo} className={backLinkClass}>
            <ArrowLeft aria-hidden="true" />
            {backLabel}
          </Link>
        ) : undefined
      }
    />
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

/** Label / value list for detail pages and the page's section card, both from `@fresh/ui`. */
export { DetailList, SectionCard as Section } from "@fresh/ui";
