import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { page } from "../test/fixtures";
import { renderApp } from "../test/render";
import { API, problem, server } from "../test/server";
import { RouteError } from "./states";

/** The illustrated card for a slot: a `data-state-slot` card holding that slot's picture. */
async function findStateCard(container: HTMLElement, slot: string): Promise<HTMLElement> {
  let card: HTMLElement | null = null;
  await waitFor(() => {
    card = container.querySelector<HTMLElement>(`[data-state-slot="${slot}"]`);
    expect(card).not.toBeNull();
  });
  const found = card as unknown as HTMLElement;
  const img = found.querySelector(`img[data-slot="${slot}"]`);
  expect(img).not.toBeNull();
  // Characters are decorative next to the card's own title, served from /illustrations at size sm (160 px).
  expect(img).toHaveAttribute("alt", "");
  expect(img?.getAttribute("src")).toMatch(/^\/illustrations\//);
  // The login side panel shows the 4:3 ADMIN_LOGIN at the panel's width; every other state is `sm`.
  if (slot !== "ADMIN_LOGIN") expect(img).toHaveAttribute("width", "160");
  return found;
}

describe("illustrated states (U3)", () => {
  it("login shows ADMIN_LOGIN inside the sign-in card", async () => {
    const { container } = renderApp({ permissions: null, path: "/login" });
    const card = await findStateCard(container, "ADMIN_LOGIN");
    expect(card).toContainElement(screen.getByRole("button", { name: "Sign in" }));
  });

  it("a route without the permission shows ADMIN_NO_ACCESS", async () => {
    const { container } = renderApp({ permissions: ["orders.view"], path: "/pricing" });
    await findStateCard(container, "ADMIN_NO_ACCESS");
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("a 403 from the API on a detail page shows ADMIN_NO_ACCESS", async () => {
    server.use(http.get(`${API}/admin/refunds/r1`, () => problem(403, "FORBIDDEN")));
    const { container } = renderApp({ permissions: ["payments.view"], path: "/refunds/r1" });
    await findStateCard(container, "ADMIN_NO_ACCESS");
  });

  it("an unknown path shows NOT_FOUND", async () => {
    const { container } = renderApp({ permissions: [], path: "/no-such-page" });
    await findStateCard(container, "NOT_FOUND");
  });

  it("a 404 on a detail page shows NOT_FOUND", async () => {
    server.use(http.get(`${API}/admin/refunds/r1`, () => problem(404, "NOT_FOUND")));
    const { container } = renderApp({ permissions: ["payments.view"], path: "/refunds/r1" });
    await findStateCard(container, "NOT_FOUND");
  });

  it("a failed load shows ERROR_GENERIC with the trace id and a retry", async () => {
    let calls = 0;
    server.use(
      http.get(`${API}/admin/refunds/r1`, () => {
        calls += 1;
        return problem(500, "INTERNAL_ERROR");
      }),
    );
    const { container, user } = renderApp({ permissions: ["payments.view"], path: "/refunds/r1" });
    const card = await findStateCard(container, "ERROR_GENERIC");
    expect(card).toHaveAttribute("role", "alert");
    expect(card).toHaveTextContent("00-test-trace-01");
    await user.click(screen.getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(calls).toBe(2));
  });

  it("an empty list shows ADMIN_EMPTY", async () => {
    server.use(http.get(`${API}/admin/settings/number-series`, () => HttpResponse.json(page([]))));
    const { container } = renderApp({
      permissions: ["settings.manage"],
      path: "/settings/number-series",
    });
    await findStateCard(container, "ADMIN_EMPTY");
  });

  it("no open payment issues shows ADMIN_ALL_CAUGHT_UP", async () => {
    server.use(http.get(`${API}/admin/payment-issues`, () => HttpResponse.json(page([]))));
    const { container } = renderApp({ permissions: ["payments.view"], path: "/payment-issues" });
    const card = await findStateCard(container, "ADMIN_ALL_CAUGHT_UP");
    expect(card).toHaveAttribute("role", "status");
  });

  it("a screen that throws shows ERROR_GENERIC from the route error boundary", async () => {
    function Boom(): never {
      throw new Error("boom");
    }
    const { container } = renderApp({
      permissions: [],
      routes: [{ path: "/", element: <Boom />, errorElement: <RouteError fullPage /> }],
    });
    const card = await findStateCard(container, "ERROR_GENERIC");
    expect(card).toHaveAttribute("role", "alert");
  });
});
