import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const PROFILE = {
  legalName: "Fresh Orbit Foods Private Limited",
  tradeName: "Fresh Orbit",
  gstin: null,
  fssaiLicenseNo: null,
  address: "12 Harbour Road",
  supportPhone: "+919800000000",
  supportEmail: "help@freshorbit.test",
  timeZone: "Asia/Kolkata",
  version: "7",
};

function captureProfilePut() {
  const seen: { ifMatch: string | null; ifNoneMatch: string | null; body: unknown }[] = [];
  server.use(
    http.put(`${API}/admin/settings/business-profile`, async ({ request }) => {
      seen.push({
        ifMatch: request.headers.get("If-Match"),
        ifNoneMatch: request.headers.get("If-None-Match"),
        body: await request.json(),
      });
      return HttpResponse.json({ ...PROFILE, version: "8" });
    }),
  );
  return seen;
}

describe("Business profile", () => {
  it("creates with If-None-Match: * when none exists", async () => {
    server.use(http.get(`${API}/admin/settings/business-profile`, () => problem(404, "NOT_FOUND")));
    const seen = captureProfilePut();
    const { user } = renderApp({
      path: "/settings/business-profile",
      permissions: ["settings.manage"],
    });
    await user.type(await screen.findByLabelText(/Legal name/), PROFILE.legalName);
    await user.type(screen.getByLabelText(/Trade name/), PROFILE.tradeName);
    await user.type(screen.getByLabelText(/Registered address/), PROFILE.address);
    await user.type(screen.getByLabelText(/Support phone/), PROFILE.supportPhone);
    await user.type(screen.getByLabelText(/Support email/), PROFILE.supportEmail);
    await user.click(screen.getByRole("button", { name: "Create profile" }));

    await waitFor(() => expect(seen).toHaveLength(1));
    expect(seen[0]).toMatchObject({
      ifMatch: null,
      ifNoneMatch: "*",
      body: { legalName: PROFILE.legalName, gstin: null, timeZone: "Asia/Kolkata" },
    });
  });

  it("updates with If-Match and shows GSTIN_INVALID at the field", async () => {
    server.use(
      http.get(`${API}/admin/settings/business-profile`, () => HttpResponse.json(PROFILE)),
    );
    const seen: (string | null)[] = [];
    server.use(
      http.put(`${API}/admin/settings/business-profile`, ({ request }) => {
        seen.push(request.headers.get("If-Match"));
        return problem(422, "GSTIN_INVALID", {
          errors: {
            gstin: [{ code: "GSTIN_INVALID", message: "Enter a valid 15-character GSTIN." }],
          },
        });
      }),
    );
    const { user } = renderApp({
      path: "/settings/business-profile",
      permissions: ["settings.manage"],
    });
    await user.type(await screen.findByLabelText(/GSTIN/), "BAD");
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText("Enter a valid 15-character GSTIN.")).toBeInTheDocument();
    expect(screen.getByLabelText(/GSTIN/)).toHaveAttribute("aria-invalid", "true");
    expect(seen).toEqual(['"7"']);
  });
});

describe("Staff", () => {
  it("shows PASSWORD_TOO_WEAK at the temporary password field", async () => {
    server.use(
      http.get(`${API}/admin/staff`, () => HttpResponse.json(page([]))),
      http.get(`${API}/admin/roles`, () =>
        HttpResponse.json({
          items: [
            {
              id: "r1",
              code: "PACKER",
              name: "Packer",
              isSystem: true,
              permissions: [],
              version: "1",
            },
          ],
        }),
      ),
      http.post(`${API}/admin/staff`, () =>
        problem(422, "PASSWORD_TOO_WEAK", {
          detail:
            "The temporaryPassword must have at least 12 characters and must not be a common password.",
          errors: {
            temporaryPassword: [
              {
                code: "PASSWORD_TOO_WEAK",
                message:
                  "The temporaryPassword must have at least 12 characters and must not be a common password.",
              },
            ],
          },
        }),
      ),
    );
    const { user } = renderApp({
      path: "/settings/staff",
      permissions: ["staff.manage"],
      allLocations: true,
    });
    await user.click(await screen.findByRole("button", { name: "New staff member" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Email/), "ravi@freshorbit.test");
    await user.type(within(dialog).getByLabelText(/Full name/), "Ravi P");
    await user.click(await within(dialog).findByRole("checkbox", { name: "Packer" }));
    await user.click(within(dialog).getByRole("switch", { name: "All locations" }));
    await user.type(within(dialog).getByLabelText(/Temporary password/), "short");
    await user.click(within(dialog).getByRole("button", { name: "Create staff member" }));

    const password = within(dialog).getByLabelText(/Temporary password/);
    await waitFor(() => expect(password).toHaveAttribute("aria-invalid", "true"));
    expect(within(dialog).getByText(/must have at least 12 characters/)).toBeInTheDocument();
  });

  it("puts PASSWORD_TOO_WEAK on the field when the server names no field", async () => {
    server.use(
      http.get(`${API}/admin/staff`, () =>
        HttpResponse.json(
          page([
            {
              id: "s1",
              userId: "u1",
              email: "ravi@freshorbit.test",
              fullName: "Ravi P",
              roles: [],
              allLocations: true,
              locationIds: [],
              isActive: true,
              userStatus: "ACTIVE",
              lastLoginAt: null,
              version: "3",
            },
          ]),
        ),
      ),
      http.post(`${API}/admin/staff/s1/reset-password`, () =>
        problem(422, "PASSWORD_TOO_WEAK", { detail: "Choose a longer password." }),
      ),
    );
    const { user } = renderApp({ path: "/settings/staff", permissions: ["staff.manage"] });
    await user.click(await screen.findByRole("button", { name: "Reset password for Ravi P" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Temporary password/), "short");
    await user.click(within(dialog).getByRole("button", { name: "Reset password" }));
    await waitFor(() =>
      expect(within(dialog).getByLabelText(/Temporary password/)).toHaveAttribute(
        "aria-invalid",
        "true",
      ),
    );
  });
});

describe("Notification templates", () => {
  it("saves with If-Match and shows an unknown placeholder at the body", async () => {
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/notification-templates`, () =>
        HttpResponse.json([
          {
            templateId: "t1",
            code: "ORDER_CONFIRMED",
            channel: "SMS",
            category: "ORDER",
            subject: null,
            body: "Order {{orderNumber}} confirmed.",
            isActive: true,
            variables: ["orderNumber"],
            dltTemplateId: null,
            version: "5",
          },
        ]),
      ),
      http.put(`${API}/admin/notification-templates/ORDER_CONFIRMED/SMS`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return problem(422, "TEMPLATE_PLACEHOLDER_UNKNOWN", {
          detail: "The body uses {{foo}}, which this template does not provide.",
        });
      }),
    );
    const { user } = renderApp({
      path: "/settings/notification-templates",
      permissions: ["notifications.manage"],
    });
    await user.click(await screen.findByRole("button", { name: "Edit Order confirmed Sms" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Message/), " {{{{foo}}");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));
    await waitFor(() =>
      expect(within(dialog).getByLabelText(/Message/)).toHaveAttribute("aria-invalid", "true"),
    );
    expect(ifMatch).toBe('"5"');
  });
});

describe("Audit log", () => {
  it("sends URL filters to the request", async () => {
    let url: URL | undefined;
    server.use(
      http.get(`${API}/admin/audit`, ({ request }) => {
        url = new URL(request.url);
        return HttpResponse.json(page([]));
      }),
    );
    renderApp({
      path: "/settings/audit?entityType=PRODUCT&fromDate=2026-10-01",
      permissions: ["audit.view"],
    });
    expect(await screen.findByText("No entries match")).toBeInTheDocument();
    expect(url?.searchParams.get("entityType")).toBe("PRODUCT");
    expect(url?.searchParams.get("fromDate")).toBe("2026-10-01");
  });
});
