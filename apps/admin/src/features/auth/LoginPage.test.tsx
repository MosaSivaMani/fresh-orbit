import { screen, waitFor, within } from "@testing-library/react";
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

  it("shows the server's wait on 429 RATE_LIMITED and keeps sign-in disabled until it passes", async () => {
    let calls = 0;
    server.use(
      http.post(`${API}/admin/auth/login`, () => {
        calls += 1;
        return problem(429, "RATE_LIMITED", { retryAfterSeconds: 2 });
      }),
    );
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "wrong");
    const submit = screen.getByRole("button", { name: "Sign in" });
    await user.click(submit);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveAttribute("data-code", "RATE_LIMITED");
    expect(within(alert).getByTestId("retry-after")).toHaveTextContent("2 s");
    expect(submit).toBeDisabled();
    await user.click(submit);
    expect(calls).toBe(1);

    await waitFor(() => expect(submit).toBeEnabled(), { timeout: 4000 });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("formats a longer wait in minutes from the Retry-After header", async () => {
    server.use(
      http.post(
        `${API}/admin/auth/login`,
        () =>
          new HttpResponse(JSON.stringify({ status: 429, code: "RATE_LIMITED", errors: {} }), {
            status: 429,
            headers: { "Content-Type": "application/problem+json", "Retry-After": "125" },
          }),
      ),
    );
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByTestId("retry-after")).toHaveTextContent("2 min 05 s");
  });

  it("has no locked or disabled account message (D33): other codes show the server sentence", async () => {
    server.use(
      http.post(`${API}/admin/auth/login`, () =>
        problem(401, "ACCOUNT_LOCKED", { title: "Server sentence.", detail: "Server sentence." }),
      ),
    );
    const { user } = renderApp({ permissions: null, path: "/login" });
    await user.type(await screen.findByLabelText(/Email/), "anitha@freshorbit.test");
    await user.type(screen.getByLabelText(/Password/), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Server sentence.");
    expect(alert.textContent).not.toMatch(/locked|disabled/i);
  });
});
