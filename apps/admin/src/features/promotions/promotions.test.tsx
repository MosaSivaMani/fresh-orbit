import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { type Coupon, type Promotion } from "../../api/types/promotions";
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
    isListed: false,
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
    await user.click(screen.getByRole("switch", { name: /Show in the customer's offers/ }));
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
      isListed: true,
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

const BANNER_ID = "0192d400-0000-7000-8000-000000000001";
const CATEGORY_ID = "0192d500-0000-7000-8000-000000000001";
const BANNER_PERMS = ["catalog.manage"] as const;

function banner(overrides: Partial<Promotion> = {}): Promotion {
  return {
    id: BANNER_ID,
    title: "Monsoon catch",
    subtitle: "Fresh prawns this week",
    imageKey: "banners/monsoon.jpg",
    linkType: "COUPON",
    linkTarget: "MONSOON15",
    cityId: null,
    startsAt: "2026-10-31T18:30:00Z",
    endsAt: null,
    sortOrder: 2,
    isActive: true,
    badge: "New",
    version: "12",
    ...overrides,
  };
}

describe("home banners (AD-6)", () => {
  it("lists the banners the server returns", async () => {
    server.use(http.get(`${API}/admin/promotions`, () => HttpResponse.json(page([banner()]))));
    renderApp({ path: "/banners", permissions: [...BANNER_PERMS] });

    expect(await screen.findByRole("link", { name: "Monsoon catch" })).toHaveAttribute(
      "href",
      `/banners/${BANNER_ID}`,
    );
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("creates a banner that opens a category, sending every field", async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/admin/categories`, () =>
        HttpResponse.json([
          {
            id: CATEGORY_ID,
            parentId: null,
            slug: "prawns",
            name: "Prawns",
            description: null,
            imageKey: null,
            sortOrder: 1,
            isActive: true,
            version: "1",
            children: [],
          },
        ]),
      ),
      http.post(`${API}/admin/promotions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(banner({ linkType: "CATEGORY", linkTarget: CATEGORY_ID }), {
          status: 201,
        });
      }),
      http.get(`${API}/admin/promotions/${BANNER_ID}`, () =>
        HttpResponse.json(banner({ linkType: "CATEGORY", linkTarget: CATEGORY_ID })),
      ),
    );
    const { user, router } = renderApp({ path: "/banners/new", permissions: [...BANNER_PERMS] });

    await user.type(await screen.findByLabelText(/^Title/), "Prawn week");
    await user.click(screen.getByRole("combobox", { name: /^Category/ }));
    await user.click(await screen.findByRole("option", { name: "Prawns" }));
    await user.type(screen.getByLabelText(/^Starts/), "2026-11-01T00:00");
    await user.click(screen.getByRole("button", { name: "Create banner" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toEqual({
      title: "Prawn week",
      subtitle: null,
      imageKey: null,
      badge: null,
      linkType: "CATEGORY",
      linkTarget: CATEGORY_ID,
      cityId: null,
      startsAt: "2026-10-31T18:30:00.000Z",
      endsAt: null,
      sortOrder: 1,
      isActive: true,
    });
    await waitFor(() => expect(router.state.location.pathname).toBe(`/banners/${BANNER_ID}`));
  });

  it("saves an edit with If-Match and keeps every field the banner had", async () => {
    let body: Record<string, unknown> | null = null;
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/promotions/${BANNER_ID}`, () =>
        HttpResponse.json(banner(), { headers: { ETag: '"12"' } }),
      ),
      http.put(`${API}/admin/promotions/${BANNER_ID}`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json(banner({ title: "Monsoon catch is here", version: "13" }));
      }),
    );
    const { user } = renderApp({
      path: `/banners/${BANNER_ID}`,
      permissions: [...BANNER_PERMS],
    });

    const title = await screen.findByLabelText(/^Title/);
    await user.clear(title);
    await user.type(title, "Monsoon catch is here");
    await user.click(screen.getByRole("button", { name: "Save banner" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(ifMatch).toBe('"12"');
    expect(body).toEqual({
      title: "Monsoon catch is here",
      subtitle: "Fresh prawns this week",
      imageKey: "banners/monsoon.jpg",
      badge: "New",
      linkType: "COUPON",
      linkTarget: "MONSOON15",
      cityId: null,
      startsAt: "2026-10-31T18:30:00.000Z",
      endsAt: null,
      sortOrder: 2,
      isActive: true,
    });
  });
});
