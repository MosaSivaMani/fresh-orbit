import { screen, waitFor, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { type CityDetail } from "../../api/types/locations";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const CITY_ID = "0192b400-0001-7000-8000-000000000001";

function city(overrides: Partial<CityDetail> = {}): CityDetail {
  return {
    id: CITY_ID,
    code: "TST",
    name: "Test City",
    stateCode: "TG",
    stateName: "Test State",
    timeZone: "Asia/Kolkata",
    status: "DRAFT",
    activatedAt: null,
    activatedBy: null,
    version: "4711",
    readiness: { cityId: CITY_ID, isReady: true, missing: [] },
    ...overrides,
  };
}

function serve(detail: CityDetail) {
  server.use(
    http.get(`${API}/admin/cities/${CITY_ID}`, () => HttpResponse.json(detail)),
    http.get(`${API}/admin/service-areas`, () => HttpResponse.json(page([]))),
  );
}

describe("CityDetailPage", () => {
  it("disables Activate and shows each missing launch check with its fix link", async () => {
    serve(
      city({
        readiness: {
          cityId: CITY_ID,
          isReady: false,
          missing: [
            {
              code: "PRICE_MISSING",
              fulfillmentLocationId: null,
              message: "12 variants have no price.",
            },
            {
              code: "SLOT_TEMPLATE_MISSING",
              fulfillmentLocationId: null,
              message: "Location A has no slots.",
            },
          ],
        },
      }),
    );
    renderApp({ path: `/cities/${CITY_ID}`, permissions: ["locations.manage", "cities.activate"] });

    expect(await screen.findByRole("button", { name: "Activate" })).toBeDisabled();
    const checklist = screen.getByRole("list", { name: "Launch checklist" });
    const price = within(checklist)
      .getByText(/Products are priced/)
      .closest("li")!;
    expect(price).toHaveTextContent("missing");
    expect(
      within(price as HTMLElement).getByRole("link", { name: "Open pricing" }),
    ).toHaveAttribute("href", "/pricing");
    const stock = within(checklist)
      .getByText(/Stock is set up/)
      .closest("li")!;
    expect(stock).toHaveTextContent("done");
  });

  it("sends If-Match when activating a ready city", async () => {
    serve(city());
    let ifMatch: string | null = null;
    server.use(
      http.post(`${API}/admin/cities/${CITY_ID}/activate`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json({ ...city(), status: "ACTIVE", version: "4712" });
      }),
    );
    const { user } = renderApp({
      path: `/cities/${CITY_ID}`,
      permissions: ["locations.manage", "cities.activate"],
    });

    const activate = await screen.findByRole("button", { name: "Activate" });
    expect(activate).toBeEnabled();
    await user.click(activate);
    const dialog = await screen.findByRole("dialog", { name: "Activate this city?" });
    await user.click(within(dialog).getByRole("button", { name: "Activate" }));

    expect(await screen.findByText("City activated")).toBeInTheDocument();
    expect(ifMatch).toBe('"4711"');
  });

  it("lists the missing items when activation is refused with CITY_NOT_READY", async () => {
    serve(city());
    server.use(
      http.post(`${API}/admin/cities/${CITY_ID}/activate`, () =>
        problem(422, "CITY_NOT_READY", {
          missing: [
            {
              code: "PINCODE_MISSING",
              fulfillmentLocationId: null,
              message: "Location B has no pincodes.",
            },
            {
              code: "STOCK_ITEM_MISSING",
              fulfillmentLocationId: null,
              message: "Location A has no stock items.",
            },
          ],
        }),
      ),
    );
    const { user } = renderApp({
      path: `/cities/${CITY_ID}`,
      permissions: ["locations.manage", "cities.activate"],
    });

    await user.click(await screen.findByRole("button", { name: "Activate" }));
    const dialog = await screen.findByRole("dialog", { name: "Activate this city?" });
    await user.click(within(dialog).getByRole("button", { name: "Activate" }));

    const missing = await within(dialog).findByRole("list", { name: "What is missing" });
    expect(
      within(missing)
        .getAllByRole("listitem")
        .map((li) => li.textContent),
    ).toEqual(["Location B has no pincodes.", "Location A has no stock items."]);
  });

  it("hides activation from staff without cities.activate", async () => {
    serve(city());
    renderApp({ path: `/cities/${CITY_ID}`, permissions: ["locations.manage"] });
    expect(await screen.findByRole("heading", { name: "Test City" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Activate" })).not.toBeInTheDocument();
  });
});

describe("City state name (AD-9)", () => {
  it("edits the state name and sends it with If-Match", async () => {
    serve(city());
    let body: Record<string, unknown> | null = null;
    let ifMatch: string | null = null;
    server.use(
      http.put(`${API}/admin/cities/${CITY_ID}`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json({ ...city({ stateName: "Other State" }), version: "4712" });
      }),
    );
    const { user } = renderApp({ path: `/cities/${CITY_ID}`, permissions: ["locations.manage"] });

    await user.click(await screen.findByRole("button", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog", { name: "Edit Test City" });
    const stateName = within(dialog).getByLabelText(/^State name/);
    expect(stateName).toHaveValue("Test State");
    await user.clear(stateName);
    await user.type(stateName, "Other State");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(ifMatch).toBe('"4711"');
    expect(body).toMatchObject({ name: "Test City", stateCode: "TG", stateName: "Other State" });
  });

  it("sends the state name when a city is created, and null when it is blank", async () => {
    const bodies: Record<string, unknown>[] = [];
    server.use(
      http.get(`${API}/admin/cities`, () => HttpResponse.json(page([]))),
      http.post(`${API}/admin/cities`, async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return problem(422, "VALIDATION_FAILED", {
          errors: { code: [{ code: "DUPLICATE", message: "Choose a code that is not used." }] },
        });
      }),
    );
    const { user } = renderApp({ path: "/cities", permissions: ["locations.manage"] });

    await user.click(await screen.findByRole("button", { name: "New city" }));
    const dialog = await screen.findByRole("dialog", { name: "New city" });
    await user.type(within(dialog).getByLabelText(/^Name/), "Test City");
    await user.type(within(dialog).getByLabelText(/^Code/), "TST");
    await user.type(within(dialog).getByLabelText(/^State code/), "TG");
    await user.type(within(dialog).getByLabelText(/^State name/), "Test State");
    await user.click(within(dialog).getByRole("button", { name: "Add city" }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    expect(bodies[0]).toMatchObject({ code: "TST", stateCode: "TG", stateName: "Test State" });

    await user.clear(within(dialog).getByLabelText(/^State name/));
    await user.click(within(dialog).getByRole("button", { name: "Add city" }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[1]).toMatchObject({ stateName: null });
  });
});
