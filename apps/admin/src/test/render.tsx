import { Toaster, TooltipProvider } from "@fresh/ui";
import { QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { type RouteObject, RouterProvider, createMemoryRouter } from "react-router";
import { type StaffProfile } from "../api/types/auth";
import { routes as appRoutes } from "../app/router";
import { ServicesProvider, createAppServices } from "../app/services";
import { type Permission } from "../auth/permissions";
import { API, API_ORIGIN, server } from "./server";

function base64Url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export const STAFF_ID = "0192c000-0000-7000-8000-000000000001";
export const LOCATION_A = "0192b400-0002-7000-8000-000000000002";
export const LOCATION_B = "0192b400-0002-7000-8000-000000000003";

/** Unsigned JWT with the staff claims the API issues (the admin never verifies signatures). */
export function fakeAccessToken(claims: {
  permissions?: readonly Permission[];
  locationIds?: readonly string[];
  allLocations?: boolean;
}): string {
  const payload: Record<string, unknown> = {
    sub: "0192c000-0000-7000-8000-0000000000aa",
    actor_type: "STAFF",
    staff_id: STAFF_ID,
    perm: claims.permissions ?? [],
    exp: Math.floor(Date.now() / 1000) + 900,
  };
  if (claims.locationIds?.length) payload.loc = claims.locationIds;
  if (claims.allLocations) payload.all_loc = "true";
  return `${base64Url({ alg: "HS256", typ: "JWT" })}.${base64Url(payload)}.signature`;
}

export function staffProfile(overrides: Partial<StaffProfile> = {}): StaffProfile {
  return {
    staffId: STAFF_ID,
    userId: "0192c000-0000-7000-8000-0000000000aa",
    email: "anitha@freshorbit.test",
    fullName: "Anitha R",
    roles: [{ id: "r1", code: "STORE_MANAGER", name: "Store manager" }],
    permissions: [],
    allLocations: false,
    locationIds: [LOCATION_A],
    ...overrides,
  };
}

export interface RenderAppOptions {
  path?: string;
  /** null = signed out. */
  permissions?: readonly Permission[] | null;
  locationIds?: readonly string[];
  allLocations?: boolean;
  routes?: RouteObject[];
}

/** Renders the real admin routes (or `routes`) at `path` with a signed-in staff session and MSW. */
export function renderApp(options: RenderAppOptions = {}) {
  const services = createAppServices({ apiOrigin: API_ORIGIN });
  const permissions = options.permissions === undefined ? [] : options.permissions;
  const locationIds = options.locationIds ?? [LOCATION_A];
  if (permissions !== null) {
    const profile = staffProfile({
      permissions: [...permissions],
      locationIds: [...locationIds],
      allLocations: options.allLocations ?? false,
    });
    services.session.signIn(
      {
        accessToken: fakeAccessToken({
          permissions,
          locationIds,
          allLocations: options.allLocations ?? false,
        }),
        refreshToken: "rt_test",
      },
      profile,
    );
    server.use(http.get(`${API}/admin/auth/me`, () => HttpResponse.json(profile)));
  }
  const router = createMemoryRouter(options.routes ?? appRoutes, {
    initialEntries: [options.path ?? "/"],
  });
  const user = userEvent.setup();
  const view = render(
    <ServicesProvider services={services}>
      <QueryClientProvider client={services.queryClient}>
        <TooltipProvider>
          <RouterProvider router={router} />
          <Toaster />
        </TooltipProvider>
      </QueryClientProvider>
    </ServicesProvider>,
  );
  return { ...view, user, services, router };
}
