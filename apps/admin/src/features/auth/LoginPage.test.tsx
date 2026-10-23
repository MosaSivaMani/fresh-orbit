import { screen } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { emptyDashboard, emptyReport } from "../../test/fixtures";
import { fakeAccessToken, renderApp, staffProfile } from "../../test/render";
import { API, problem, server } from "../../test/server";

describe("LoginPage", () => {
  it("validates the form before calling the API", async () => {
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.click(await screen.findByRole("button", { name: "Sign in" }));
    expect(await screen.findByText("Enter your email address.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Email/)).toHaveAttribute("aria-invalid", "true");
  });

  it("signs in and returns to the page the staff member asked for", async () => {
    let body: unknown;
    server.use(
      http.post(`${API}/admin/auth/login`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({
          accessToken: fakeAccessToken({ permissions: ["reports.view"] }),
          accessTokenExpiresAt: "2026-10-24T06:00:00Z",
          refreshToken: "rt_1",
          refreshTokenExpiresAt: "2026-10-24T18:00:00Z",
          staff: staffProfile({ permissions: ["reports.view"] }),
        });
      }),
      http.get(`${API}/admin/auth/me`, () => HttpResponse.json(staffProfile())),
      http.get(`${API}/admin/reports/dashboard`, () => HttpResponse.json(emptyDashboard())),
      http.get(`${API}/admin/reports/daily-sales`, () => HttpResponse.json(emptyReport())),
    );
    const { user, router, services } = renderApp({ permissions: null, path: "/login?next=%2F" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "correct horse battery");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { name: "Dashboard" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/");
    expect(body).toMatchObject({
      email: "anitha@freshorbit.test",
      password: "correct horse battery",
    });
    expect(services.session.getState().status).toBe("signed-in");
  });

  it("shows a refused sign-in by its code", async () => {
    server.use(http.post(`${API}/admin/auth/login`, () => problem(401, "INVALID_CREDENTIALS")));
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The email or password is not correct.",
    );
  });

  it("puts server field errors on the matching field", async () => {
    server.use(
      http.post(`${API}/admin/auth/login`, () =>
        problem(422, "VALIDATION_FAILED", {
          errors: {
            email: [{ code: "EMAIL_TOO_LONG", message: "Email must be at most 254 characters." }],
          },
        }),
      ),
    );
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "x");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByText("Email must be at most 254 characters.")).toBeInTheDocument();
  });
});
