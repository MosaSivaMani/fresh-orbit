import { render, screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { MONITORING } from "../../test/msw/handlers";
import { server } from "../../test/msw/server";
import RouteError from "./error";
import NotFound from "./not-found";

describe("NotFound", () => {
  it("shows the not-found character in its circle, an h1 and two ways on", () => {
    const { container } = render(<NotFound />);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("This page swam away");
    const img = container.querySelector("[data-avatar-size] img");
    expect(img?.getAttribute("src")).toContain("not_found");
    expect(screen.getByRole("link", { name: "Browse the shop" })).toHaveAttribute("href", "/shop");
    expect(screen.getByRole("link", { name: "Go home" })).toHaveAttribute("href", "/");
  });
});

describe("RouteError", () => {
  it("shows the error character with the reference and retries", () => {
    const reset = vi.fn();
    vi.spyOn(console, "error").mockImplementation(() => {});
    render(<RouteError error={Object.assign(new Error("x"), { digest: "d-1" })} reset={reset} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Reference: d-1");
    screen.getByRole("button", { name: "Try again" }).click();
    expect(reset).toHaveBeenCalledOnce();
  });

  it("reports what it caught to the site's error monitoring, with the reference (W-7)", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const reports: Record<string, unknown>[] = [];
    server.use(
      http.post(MONITORING, async ({ request }) => {
        expect(request.headers.get("X-Fresh-Client")).toBe("web");
        reports.push((await request.json()) as Record<string, unknown>);
        return new HttpResponse(null, { status: 204 });
      }),
    );

    render(<RouteError error={Object.assign(new Error("x"), { digest: "d-1" })} reset={vi.fn()} />);

    await waitFor(() => expect(reports).toHaveLength(1));
    expect(reports[0]).toMatchObject({ digest: "d-1", boundary: "route", path: "/" });
  });
});
