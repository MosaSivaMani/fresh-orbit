import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { problem } from "../../../test/fixtures";
import { API, BFF } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { routerMock } from "../../../test/router-mock";
import { sessionStore } from "@/auth/session-store";
import { LoginForm, safeNext } from "./login-form";

const challenge = {
  challengeId: "ch1",
  expiresAt: "2026-10-23T05:05:00Z",
  resendAfterSeconds: 30,
  codeLength: 6,
};

async function reachCodeStep() {
  await userEvent.type(screen.getByLabelText(/Mobile number/), "9876543210");
  await userEvent.click(screen.getByRole("button", { name: "Send code" }));
  return screen.findByLabelText("One-time code");
}

describe("LoginForm", () => {
  it("requests a code for the +91 number, verifies through the BFF and returns to `next`", async () => {
    let requested: unknown;
    let verified: { body: unknown; client: string | null } | null = null;
    server.use(
      http.post(`${API}/auth/otp/request`, async ({ request }) => {
        requested = await request.json();
        return HttpResponse.json(challenge, { status: 202 });
      }),
      http.post(`${BFF}/verify`, async ({ request }) => {
        verified = { body: await request.json(), client: request.headers.get("X-Fresh-Client") };
        return HttpResponse.json({ ...signedIn, cart: null });
      }),
    );
    renderWithProviders(<LoginForm next="/checkout" />);

    await userEvent.type(await reachCodeStep(), "123456");
    expect(requested).toEqual({ phone: "+919876543210", purpose: "LOGIN" });
    expect(screen.getByText(/new code in 30 seconds/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Verify and sign in" }));

    await waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith("/checkout"));
    expect(verified).toEqual({
      body: { challengeId: "ch1", phone: "+919876543210", code: "123456" },
      client: "web",
    });
    expect(sessionStore.getState().status).toBe("signedIn");
    // The access token is held in memory only.
    expect(sessionStore.getAccessToken()).toBe("access-1");
    expect(window.localStorage.length).toBe(0);
  });

  it("shows the server's message under the code field for OTP_INVALID and stays signed out", async () => {
    server.use(
      http.post(`${API}/auth/otp/request`, () => HttpResponse.json(challenge, { status: 202 })),
      http.post(`${BFF}/verify`, () =>
        HttpResponse.json(
          problem(422, "OTP_INVALID", {
            errors: { code: [{ code: "OTP_INVALID", message: "The code is not correct." }] },
          }),
          { status: 422 },
        ),
      ),
    );
    renderWithProviders(<LoginForm next={null} />);

    const codeInput = await reachCodeStep();
    await userEvent.type(codeInput, "000000");
    await userEvent.click(screen.getByRole("button", { name: "Verify and sign in" }));

    expect(await screen.findByText("The code is not correct.")).toBeInTheDocument();
    expect(codeInput).toHaveAttribute("aria-invalid", "true");
    expect(sessionStore.getState().status).toBe("signedOut");
    expect(routerMock.replace).not.toHaveBeenCalled();
  });

  it("refuses a malformed number without calling the server", async () => {
    renderWithProviders(<LoginForm next={null} />);
    await userEvent.type(screen.getByLabelText(/Mobile number/), "12345");
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));
    expect(screen.getByText("Enter your 10-digit mobile number.")).toBeInTheDocument();
  });

  it("on 429 waits for Retry-After before allowing another code request", async () => {
    server.use(
      http.post(`${API}/auth/otp/request`, () =>
        HttpResponse.json(problem(429, "RATE_LIMITED"), {
          status: 429,
          headers: { "Retry-After": "42" },
        }),
      ),
    );
    renderWithProviders(<LoginForm next={null} />);
    await userEvent.type(screen.getByLabelText(/Mobile number/), "9876543210");
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/wait 4[12] seconds/);
    expect(screen.getByRole("button", { name: "Send code" })).toBeDisabled();
    // Still on the phone step, number kept.
    expect(screen.getByLabelText(/Mobile number/)).toHaveValue("9876543210");
  });

  it("on 503 SMS_PROVIDER_UNAVAILABLE shows the server's message and resends to the same number", async () => {
    const requests: unknown[] = [];
    server.use(
      http.post(`${API}/auth/otp/request`, async ({ request }) => {
        requests.push(await request.json());
        if (requests.length === 1) {
          return HttpResponse.json(
            problem(503, "SMS_PROVIDER_UNAVAILABLE", {
              detail: "The SMS could not be sent; try again in a few minutes.",
            }),
            { status: 503 },
          );
        }
        return HttpResponse.json(challenge, { status: 202 });
      }),
    );
    renderWithProviders(<LoginForm next={null} />);
    await userEvent.type(screen.getByLabelText(/Mobile number/), "9876543210");
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("The SMS could not be sent; try again in a few minutes.");
    await userEvent.click(screen.getByRole("button", { name: "Try sending again" }));

    expect(await screen.findByLabelText("One-time code")).toBeInTheDocument();
    expect(requests).toEqual([
      { phone: "+919876543210", purpose: "LOGIN" },
      { phone: "+919876543210", purpose: "LOGIN" },
    ]);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("follows only same-site paths after sign-in", () => {
    expect(safeNext("/orders/1")).toBe("/orders/1");
    expect(safeNext("//evil.test")).toBe("/");
    expect(safeNext("https://evil.test")).toBe("/");
    expect(safeNext(null)).toBe("/");
  });
});
