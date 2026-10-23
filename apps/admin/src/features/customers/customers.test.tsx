import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const CUSTOMER_ID = "0192d000-0000-7000-8000-000000000010";

function customerDetail(overrides: Record<string, unknown> = {}) {
  return {
    customerId: CUSTOMER_ID,
    userId: "0192d000-0000-7000-8000-000000000011",
    fullName: "Lakshmi K",
    phone: "+919800000001",
    email: null,
    marketingOptIn: false,
    status: "ACTIVE",
    isBlocked: false,
    deletedAt: null,
    addresses: [],
    version: "41",
    ...overrides,
  };
}

describe("Customers", () => {
  it("sends search and status filters from the URL to the list request", async () => {
    let url: URL | undefined;
    server.use(
      http.get(`${API}/admin/customers`, ({ request }) => {
        url = new URL(request.url);
        return HttpResponse.json(
          page([
            {
              customerId: CUSTOMER_ID,
              fullName: "Lakshmi K",
              phone: "+919800000001",
              email: null,
              status: "ACTIVE",
              isBlocked: true,
            },
          ]),
        );
      }),
    );
    renderApp({ path: "/customers?q=lak&status=ACTIVE", permissions: ["customers.view"] });
    const link = await screen.findByRole("link", { name: "Lakshmi K" });
    expect(link).toHaveAttribute("href", `/customers/${CUSTOMER_ID}`);
    expect(url?.searchParams.get("q")).toBe("lak");
    expect(url?.searchParams.get("status")).toBe("ACTIVE");
  });

  it("blocks a customer with If-Match after confirmation", async () => {
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/customers/${CUSTOMER_ID}`, () =>
        HttpResponse.json(customerDetail(), { headers: { ETag: '"41"' } }),
      ),
      http.post(`${API}/admin/customers/${CUSTOMER_ID}/block`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json(customerDetail({ isBlocked: true, version: "42" }));
      }),
    );
    const { user } = renderApp({
      path: `/customers/${CUSTOMER_ID}`,
      permissions: ["customers.view", "customers.manage"],
    });
    await user.click(await screen.findByRole("button", { name: "Block" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Block customer" }));
    expect(await screen.findByRole("button", { name: "Unblock" })).toBeInTheDocument();
    expect(ifMatch).toBe('"41"');
  });

  it("hides block for staff without customers.manage", async () => {
    server.use(
      http.get(`${API}/admin/customers/${CUSTOMER_ID}`, () => HttpResponse.json(customerDetail())),
    );
    renderApp({ path: `/customers/${CUSTOMER_ID}`, permissions: ["customers.view"] });
    expect(await screen.findByRole("heading", { name: "Lakshmi K" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Block" })).not.toBeInTheDocument();
  });

  it("shows not found for an unknown customer", async () => {
    server.use(
      http.get(`${API}/admin/customers/${CUSTOMER_ID}`, () => problem(404, "CUSTOMER_NOT_FOUND")),
    );
    renderApp({ path: `/customers/${CUSTOMER_ID}`, permissions: ["customers.view"] });
    expect(await screen.findByText("This customer was not found")).toBeInTheDocument();
  });
});
