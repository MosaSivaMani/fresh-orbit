import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const LETTER_ID = "0192d000-0000-7000-8000-0000000000d1";
const LOCATION_ID = "0192b400-0002-7000-8000-0000000000a1";
const DETAILS = /^Details of Shipment delivered/;

function deadLetter(overrides: Record<string, unknown> = {}) {
  return {
    id: LETTER_ID,
    eventId: "0192d000-0000-7000-8000-0000000000e1",
    eventType: "FreshOrbit.Ordering.Public.Events.ShipmentDelivered, FreshOrbit.Ordering.Public",
    handler: "FreshOrbit.Ordering.Application.Handlers.IssueInvoiceOnShipmentDelivered",
    error: "FreshOrbit.SharedKernel.Errors.DomainException: CONFIGURATION_MISSING gstin",
    failedAt: "2026-11-09T05:00:00Z",
    attempts: 15,
    replayedAt: null,
    replayedBy: null,
    eventOccurredAt: "2026-11-08T22:30:00Z",
    correlationId: "00-abc-01",
    ...overrides,
  };
}

describe("Failed messages", () => {
  it("lists open dead letters by default with readable event and handler names", async () => {
    let url: URL | undefined;
    server.use(
      http.get(`${API}/admin/dead-letters`, ({ request }) => {
        url = new URL(request.url);
        return HttpResponse.json(page([deadLetter()]));
      }),
    );
    renderApp({ path: "/settings/failed-messages", permissions: ["platform.manage"] });

    expect(await screen.findByText("Shipment delivered")).toBeInTheDocument();
    expect(screen.getByText("IssueInvoiceOnShipmentDelivered")).toBeInTheDocument();
    expect(screen.getByText("15")).toBeInTheDocument();
    expect(url?.searchParams.get("status")).toBe("OPEN");
  });

  it("sends the replayed filter from the URL and no status for all", async () => {
    const statuses: (string | null)[] = [];
    server.use(
      http.get(`${API}/admin/dead-letters`, ({ request }) => {
        statuses.push(new URL(request.url).searchParams.get("status"));
        return HttpResponse.json(page([]));
      }),
    );
    const first = renderApp({
      path: "/settings/failed-messages?status=REPLAYED",
      permissions: ["platform.manage"],
    });
    expect(await screen.findByText("No failed messages match")).toBeInTheDocument();
    expect(statuses).toContain("REPLAYED");
    first.unmount();

    renderApp({ path: "/settings/failed-messages?status=ALL", permissions: ["platform.manage"] });
    expect(await screen.findByText("No failed messages match")).toBeInTheDocument();
    expect(statuses.at(-1)).toBeNull();
  });

  it("shows the details and replays only after confirmation", async () => {
    let replays = 0;
    server.use(
      http.get(`${API}/admin/dead-letters`, () => HttpResponse.json(page([deadLetter()]))),
      http.get(`${API}/admin/dead-letters/${LETTER_ID}`, () => HttpResponse.json(deadLetter())),
      http.post(`${API}/admin/dead-letters/${LETTER_ID}/replay`, () => {
        replays += 1;
        return HttpResponse.json(
          deadLetter({ attempts: 0, replayedAt: "2026-11-09T06:00:00Z", replayedBy: "u1" }),
        );
      }),
    );
    const { user } = renderApp({
      path: "/settings/failed-messages",
      permissions: ["platform.manage"],
    });

    await user.click(await screen.findByRole("button", { name: DETAILS }));
    const dialog = await screen.findByRole("dialog", { name: "Failed message" });
    expect(await within(dialog).findByText("00-abc-01")).toBeInTheDocument();
    expect(within(dialog).getByText(/CONFIGURATION_MISSING gstin/)).toBeInTheDocument();

    await user.click(within(dialog).getByRole("button", { name: "Replay" }));
    const confirm = await screen.findByRole("dialog", { name: "Replay this event?" });
    await user.click(within(confirm).getByRole("button", { name: "Keep as is" }));
    expect(replays).toBe(0);

    await user.click(within(dialog).getByRole("button", { name: "Replay" }));
    const confirmAgain = await screen.findByRole("dialog", { name: "Replay this event?" });
    await user.click(within(confirmAgain).getByRole("button", { name: "Replay now" }));
    expect(await screen.findByText("Event put back in the queue")).toBeInTheDocument();
    expect(replays).toBe(1);
  });

  it("offers no replay for a dead letter already replayed", async () => {
    const replayed = deadLetter({ replayedAt: "2026-11-09T06:00:00Z", replayedBy: "u1" });
    server.use(
      http.get(`${API}/admin/dead-letters`, () => HttpResponse.json(page([replayed]))),
      http.get(`${API}/admin/dead-letters/${LETTER_ID}`, () => HttpResponse.json(replayed)),
    );
    const { user } = renderApp({
      path: "/settings/failed-messages?status=REPLAYED",
      permissions: ["platform.manage"],
    });
    await user.click(await screen.findByRole("button", { name: DETAILS }));
    const dialog = await screen.findByRole("dialog", { name: "Failed message" });
    await within(dialog).findByText("00-abc-01");
    expect(within(dialog).queryByRole("button", { name: "Replay" })).not.toBeInTheDocument();
  });

  it("shows the server refusal when the replay conflicts", async () => {
    server.use(
      http.get(`${API}/admin/dead-letters`, () => HttpResponse.json(page([deadLetter()]))),
      http.get(`${API}/admin/dead-letters/${LETTER_ID}`, () => HttpResponse.json(deadLetter())),
      http.post(`${API}/admin/dead-letters/${LETTER_ID}/replay`, () =>
        problem(409, "DEAD_LETTER_ALREADY_REPLAYED", {
          detail: "This dead letter was already replayed; check the deadLetterId.",
        }),
      ),
    );
    const { user } = renderApp({
      path: "/settings/failed-messages",
      permissions: ["platform.manage"],
    });
    await user.click(await screen.findByRole("button", { name: DETAILS }));
    const dialog = await screen.findByRole("dialog", { name: "Failed message" });
    await user.click(await within(dialog).findByRole("button", { name: "Replay" }));
    const confirmAgain = await screen.findByRole("dialog", { name: "Replay this event?" });
    await user.click(within(confirmAgain).getByRole("button", { name: "Replay now" }));
    expect(await screen.findByText("Could not replay")).toBeInTheDocument();
  });

  it("is not available without platform.manage", async () => {
    renderApp({ path: "/settings/failed-messages", permissions: ["settings.manage"] });
    expect(await screen.findByText("You do not have access to this page")).toBeInTheDocument();
  });
});

function readiness(overrides: Record<string, unknown> = {}) {
  return {
    isReady: false,
    businessProfile: [
      { field: "legalName", status: "OK" },
      { field: "address", status: "OK" },
      { field: "gstin", status: "MISSING" },
      { field: "timeZone", status: "OK" },
    ],
    locations: [
      {
        locationId: LOCATION_ID,
        cityId: "0192b400-0001-7000-8000-0000000000c1",
        code: "CHN-TVM",
        name: "Thiruvanmiyur",
        locationStatus: "ACTIVE",
        fssaiLicenseStatus: "MISSING",
      },
    ],
    channels: [
      { channel: "SMS", provider: "DLT", status: "OK" },
      { channel: "EMAIL", provider: "DISABLED", status: "MISSING" },
      { channel: "PUSH", provider: "DEVELOPMENT", status: "MISSING" },
    ],
    ...overrides,
  };
}

describe("Store readiness", () => {
  it("marks each missing item and links to where it is fixed", async () => {
    server.use(
      http.get(`${API}/admin/settings/store-readiness`, () => HttpResponse.json(readiness())),
    );
    renderApp({
      path: "/settings/store-readiness",
      permissions: ["settings.manage", "locations.manage"],
    });

    expect(await screen.findByText("Not ready")).toBeInTheDocument();
    const gstin = screen.getByRole("listitem", { name: "GSTIN" });
    expect(within(gstin).getByText("Missing")).toBeInTheDocument();
    const profileLink = within(gstin).getByRole("link", { name: "Open business profile" });
    expect(profileLink).toHaveAttribute("href", "/settings/business-profile");
    const legalName = screen.getByRole("listitem", { name: "Legal name" });
    expect(within(legalName).getByText("OK")).toBeInTheDocument();
    expect(within(legalName).queryByRole("link")).not.toBeInTheDocument();

    const location = screen.getByRole("listitem", {
      name: "Thiruvanmiyur (CHN-TVM) FSSAI licence",
    });
    const locationLink = within(location).getByRole("link", { name: "Open Thiruvanmiyur" });
    expect(locationLink).toHaveAttribute("href", `/locations/${LOCATION_ID}`);

    const email = screen.getByRole("listitem", { name: "E-mail" });
    expect(within(email).getByText("Switched off")).toBeInTheDocument();
    expect(within(email).getByText("Missing")).toBeInTheDocument();
    const sms = screen.getByRole("listitem", { name: "SMS (DLT)" });
    expect(within(sms).getByText("OK")).toBeInTheDocument();
  });

  it("reports a store with no location and hides links staff cannot use", async () => {
    server.use(
      http.get(`${API}/admin/settings/store-readiness`, () =>
        HttpResponse.json(readiness({ locations: [] })),
      ),
    );
    renderApp({ path: "/settings/store-readiness", permissions: ["settings.manage"] });
    const none = await screen.findByRole("listitem", { name: "No location yet" });
    expect(within(none).getByText("Missing")).toBeInTheDocument();
    expect(within(none).queryByRole("link")).not.toBeInTheDocument();
  });

  it("shows ready when the server says so", async () => {
    const ok = readiness({
      isReady: true,
      businessProfile: [
        { field: "legalName", status: "OK" },
        { field: "address", status: "OK" },
        { field: "gstin", status: "OK" },
        { field: "timeZone", status: "OK" },
      ],
      locations: [
        {
          locationId: LOCATION_ID,
          cityId: "0192b400-0001-7000-8000-0000000000c1",
          code: "CHN-TVM",
          name: "Thiruvanmiyur",
          locationStatus: "ACTIVE",
          fssaiLicenseStatus: "OK",
        },
      ],
      channels: [
        { channel: "SMS", provider: "DLT", status: "OK" },
        { channel: "EMAIL", provider: "SMTP", status: "OK" },
        { channel: "PUSH", provider: "FCM", status: "OK" },
      ],
    });
    server.use(http.get(`${API}/admin/settings/store-readiness`, () => HttpResponse.json(ok)));
    renderApp({ path: "/settings/store-readiness", permissions: ["settings.manage"] });
    expect(await screen.findByText("Ready to trade")).toBeInTheDocument();
    expect(screen.queryByText("Not ready")).not.toBeInTheDocument();
    expect(screen.queryByText("Missing")).not.toBeInTheDocument();
  });
});
