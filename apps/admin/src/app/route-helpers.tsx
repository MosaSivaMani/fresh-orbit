import { EmptyState } from "@fresh/ui";
import { Construction } from "lucide-react";
import { type ComponentType } from "react";
import { type RouteObject } from "react-router";
import { type PermissionRequirement } from "../auth/permissions";
import { PageHeader, RequirePermission } from "../components/page";

/**
 * A route whose screen is code-split and shown only to staff holding `perm` (others get the "no access" page;
 * the API enforces the same permission).
 */
export function screen(
  path: string | undefined,
  perm: PermissionRequirement | undefined,
  load: () => Promise<ComponentType>,
): RouteObject {
  return {
    ...(path === undefined ? { index: true } : { path }),
    lazy: async () => {
      const Screen = await load();
      return {
        Component: function GuardedScreen() {
          return (
            <RequirePermission perm={perm}>
              <Screen />
            </RequirePermission>
          );
        },
      };
    },
  } as RouteObject;
}

/** Placeholder for a screen that is listed in docs/design/admin.md but not built yet. */
export function NotBuiltYet({ title }: { title: string }) {
  return (
    <>
      <PageHeader title={title} />
      <EmptyState
        icon={<Construction aria-hidden="true" />}
        title="This screen is not built yet"
        description="It is planned for Phase 6; see docs/design/admin.md for the screen inventory."
      />
    </>
  );
}

export function notBuilt(
  path: string,
  perm: PermissionRequirement | undefined,
  title: string,
): RouteObject {
  return screen(
    path,
    perm,
    async () =>
      function Placeholder() {
        return <NotBuiltYet title={title} />;
      },
  );
}
