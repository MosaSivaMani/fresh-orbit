import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";

/** Base URL the test app uses (`createAppServices({ apiOrigin: API_ORIGIN })`). */
export const API_ORIGIN = "http://api.test";
export const API = `${API_ORIGIN}/api/v1`;

/** MSW server mocking FreshOrbit.Api per docs/api. Tests add handlers with `server.use(...)`. */
export const server = setupServer(
  // The shell loads the location names (for locations.manage) on every screen; renderApp adds /admin/auth/me.
  http.get(`${API}/admin/fulfillment-locations`, () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
  ),
);

/** RFC 7807 Problem Details response as the API sends it (docs/api §1.6). */
export function problem(
  status: number,
  code: string,
  extra: Record<string, unknown> = {},
): HttpResponse<Record<string, unknown>> {
  return HttpResponse.json(
    {
      type: `https://api.freshorbit.in/problems/${code.toLowerCase().replace(/_/g, "-")}`,
      title: "Request refused.",
      status,
      code,
      traceId: "00-test-trace-01",
      errors: {},
      ...extra,
    },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );
}
