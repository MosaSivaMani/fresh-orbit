import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { type Coupon } from "../../api/types/promotions";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const COUPON_ID = "0192d300-0000-7000-8000-000000000001";

function coupon(overrides: Partial<Coupon> = {}): Coupon {
  return {
    id: COUPON_ID,
    code: "FRESH100",
    description: "₹100 off your first order",
    discountType: "FLAT",
    discountValue: 10000,
    maxDiscountPaise: null,
    minOrderPaise: 50000,
    cityId: null,
    firstOrderOnly: true,
    perCustomerLimit: 1,
    totalLimit: 500,
    usedCount: 3,
    validFrom: "2026-10-01T00:00:00Z",
    validTo: "2026-12-31T18:29:00Z",
    status: "ACTIVE",
    scopes: [],
    version: "7",
    ...overrides,
  };
}

const PERMS = ["promotions.manage"] as const;

describe("coupons", () => {
  it("lists coupons with the server's discount and usage", async () => {
    server.use(http.get(`${API}/admin/coupons`, () => HttpResponse.json(page([coupon()]))));
    renderApp({ path: "/promotions", permissions: [...PERMS] });

    expect(await screen.findByRole("link", { name: "FRESH100" })).toBeInTheDocument();
    expect(screen.getByText("3 / 500")).toBeInTheDocument();
  });

  it("creates a percentage coupon in basis points and paise, and shows a duplicate code at the field", async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/admin/coupons`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return problem(422, "VALIDATION_FAILED", {
          errors: {
            code: [{ code: "DUPLICATE", message: "Choose a code that is not already used." }],
          },
        });
      }),
    );
    const { user } = renderApp({ path: "/promotions/new", permissions: [...PERMS] });

    await user.type(await screen.findByLabelText(/^Code/), "monsoon15");
    await user.type(screen.getByLabelText(/^Description/), "15% off seafood");
    await user.type(screen.getByLabelText(/^Discount \(%\)/), "15");
    await user.type(screen.getByLabelText(/^Largest discount/), "150");
    await user.type(screen.getByLabelText(/^Starts/), "2026-11-01T00:00");
    await user.type(screen.getByLabelText(/^Ends/), "2026-11-30T23:59");
    await user.click(screen.getByRole("button", { name: "Create coupon" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({
      code: "MONSOON15",
      discountType: "PERCENT",
      discountValue: 1500,
      maxDiscountPaise: 15000,
      minOrderPaise: 0,
      cityId: null,
      perCustomerLimit: 1,
      totalLimit: null,
      validFrom: "2026-10-31T18:30:00.000Z",
      scopes: [],
    });
    await waitFor(() =>
      expect(screen.getByLabelText(/^Code/)).toHaveAttribute("aria-invalid", "true"),
    );
  });

  it("pauses an active coupon with If-Match", async () => {
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/coupons/${COUPON_ID}`, () => HttpResponse.json(coupon())),
      http.get(`${API}/admin/coupons/${COUPON_ID}/redemptions`, () => HttpResponse.json(page([]))),
      http.post(`${API}/admin/coupons/${COUPON_ID}/pause`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json(coupon({ status: "PAUSED", version: "8" }));
      }),
    );
    const { user } = renderApp({ path: `/promotions/${COUPON_ID}`, permissions: [...PERMS] });

    await user.click(await screen.findByRole("button", { name: "Pause" }));
    await waitFor(() => expect(ifMatch).toBe('"7"'));
    expect(screen.getByLabelText(/^Code/)).toBeDisabled();
  });
});
