import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { type AdminCategory, type AdminProduct } from "../../api/types/catalog";
import { page } from "../../test/fixtures";
import { renderApp } from "../../test/render";
import { API, problem, server } from "../../test/server";

const PRODUCT_ID = "0192d100-0000-7000-8000-000000000001";
const CATEGORY_ID = "0192d100-0000-7000-8000-000000000100";

const category: AdminCategory = {
  id: CATEGORY_ID,
  parentId: null,
  slug: "sea-fish",
  name: "Sea fish",
  description: null,
  imageKey: null,
  sortOrder: 1,
  isActive: true,
  version: "3",
  children: [],
};

function product(overrides: Partial<AdminProduct> = {}): AdminProduct {
  return {
    id: PRODUCT_ID,
    categoryId: CATEGORY_ID,
    slug: "seer-fish",
    name: "Seer fish",
    localNames: ["Vanjaram", "Surmai"],
    speciesScientificName: null,
    description: null,
    sourceType: "WILD",
    sourceRegion: null,
    storageInstructions: null,
    handlingNotes: null,
    hsnCode: "0302",
    taxCategoryCode: "GST_FRESH_FISH",
    status: "DRAFT",
    featuredRank: null,
    publishedAt: null,
    version: "41",
    variants: [
      {
        id: "variant-1",
        sku: "SEER-STEAK",
        name: "Steaks",
        sellUnit: "KG",
        stockUnit: "GRAM",
        packSizeGrams: null,
        avgPieceGrams: null,
        minOrder: 250,
        maxOrder: 5000,
        orderStep: 250,
        isWeightVariable: true,
        isActive: true,
        preparations: [
          { code: "STEAKS", name: "Steaks", yieldBp: 7000, isDefault: true, isActive: true },
        ],
      },
    ],
    images: [],
    ...overrides,
  };
}

function catalogHandlers() {
  server.use(http.get(`${API}/admin/categories`, () => HttpResponse.json([category])));
}

describe("products", () => {
  it("lists products with their category and status", async () => {
    catalogHandlers();
    let query: URLSearchParams | undefined;
    server.use(
      http.get(`${API}/admin/products`, ({ request }) => {
        query = new URL(request.url).searchParams;
        return HttpResponse.json(
          page([
            {
              id: PRODUCT_ID,
              categoryId: CATEGORY_ID,
              slug: "seer-fish",
              name: "Seer fish",
              status: "ACTIVE",
              activeVariantCount: 2,
              imageUrl: null,
              publishedAt: "2026-10-01T04:00:00Z",
              version: "41",
            },
          ]),
        );
      }),
    );
    renderApp({ path: "/products?status=ACTIVE", permissions: ["catalog.manage"] });

    expect(await screen.findByRole("link", { name: "Seer fish" })).toHaveAttribute(
      "href",
      `/products/${PRODUCT_ID}`,
    );
    expect(screen.getAllByText("Sea fish").length).toBeGreaterThan(0);
    expect(query?.get("status")).toBe("ACTIVE");
  });

  it("saves details with If-Match and shows a server field error at the field", async () => {
    catalogHandlers();
    let ifMatch: string | null = null;
    let body: Record<string, unknown> | undefined;
    server.use(
      http.get(`${API}/admin/products/${PRODUCT_ID}`, () => HttpResponse.json(product())),
      http.put(`${API}/admin/products/${PRODUCT_ID}`, async ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        body = (await request.json()) as Record<string, unknown>;
        return problem(409, "SLUG_TAKEN", {
          errors: {
            slug: [{ code: "SLUG_TAKEN", message: "Another product already uses this slug." }],
          },
        });
      }),
    );
    const { user } = renderApp({
      path: `/products/${PRODUCT_ID}`,
      permissions: ["catalog.manage"],
    });

    const slug = await screen.findByLabelText(/URL slug/);
    await user.clear(slug);
    await user.type(slug, "king-fish");
    await user.click(screen.getByRole("button", { name: "Save details" }));

    expect(await screen.findByText("Another product already uses this slug.")).toBeInTheDocument();
    expect(slug).toHaveAttribute("aria-invalid", "true");
    expect(ifMatch).toBe('"41"');
    expect(body).toMatchObject({
      slug: "king-fish",
      localNames: ["Vanjaram", "Surmai"],
      categoryId: CATEGORY_ID,
    });
  });

  it("publishes a draft with the product's version", async () => {
    catalogHandlers();
    let ifMatch: string | null = null;
    server.use(
      http.get(`${API}/admin/products/${PRODUCT_ID}`, () => HttpResponse.json(product())),
      http.post(`${API}/admin/products/${PRODUCT_ID}/publish`, ({ request }) => {
        ifMatch = request.headers.get("If-Match");
        return HttpResponse.json(product({ status: "ACTIVE", version: "42" }));
      }),
    );
    const { user } = renderApp({
      path: `/products/${PRODUCT_ID}`,
      permissions: ["catalog.manage"],
    });

    await user.click(await screen.findByRole("button", { name: "Publish" }));

    expect(await screen.findByRole("button", { name: "Archive" })).toBeInTheDocument();
    expect(ifMatch).toBe('"41"');
  });

  it("shows a server yield error at the preparation's yield field", async () => {
    catalogHandlers();
    server.use(
      http.get(`${API}/admin/products/${PRODUCT_ID}`, () => HttpResponse.json(product())),
      http.get(`${API}/admin/preparation-methods`, () =>
        HttpResponse.json([
          {
            id: "p1",
            code: "STEAKS",
            name: "Steaks",
            description: null,
            sortOrder: 1,
            guidanceYieldBp: 7000,
          },
        ]),
      ),
      http.put(`${API}/admin/products/${PRODUCT_ID}/variants/variant-1`, () =>
        problem(422, "YIELD_INVALID", {
          errors: {
            "preparations[0].yieldBp": [
              { code: "YIELD_INVALID", message: "Yield must be at most 100 %." },
            ],
          },
        }),
      ),
    );
    const { user } = renderApp({
      path: `/products/${PRODUCT_ID}`,
      permissions: ["catalog.manage"],
    });

    await user.click(await screen.findByRole("tab", { name: /Variants/ }));
    await user.click(screen.getByRole("button", { name: "Edit variant SEER-STEAK" }));
    await user.click(await screen.findByRole("button", { name: "Save variant" }));

    await waitFor(() =>
      expect(screen.getByText("Yield must be at most 100 %.")).toBeInTheDocument(),
    );
    expect(screen.getByLabelText(/Yield %/)).toHaveAttribute("aria-invalid", "true");
  });

  it("loads and sends the variant's serving facts", async () => {
    catalogHandlers();
    const saved = product().variants[0]!;
    let sent: Record<string, unknown> | undefined;
    server.use(
      http.get(`${API}/admin/products/${PRODUCT_ID}`, () =>
        HttpResponse.json(product({ variants: [{ ...saved, piecesMin: 10, serves: 4 }] })),
      ),
      http.get(`${API}/admin/preparation-methods`, () => HttpResponse.json([])),
      http.put(`${API}/admin/products/${PRODUCT_ID}/variants/variant-1`, async ({ request }) => {
        sent = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(product());
      }),
    );
    const { user } = renderApp({
      path: `/products/${PRODUCT_ID}`,
      permissions: ["catalog.manage"],
    });

    await user.click(await screen.findByRole("tab", { name: /Variants/ }));
    await user.click(screen.getByRole("button", { name: "Edit variant SEER-STEAK" }));
    expect(await screen.findByLabelText(/Fewest pieces \(per kg net\)/)).toHaveValue("10");
    await user.type(screen.getByLabelText(/Most pieces/), "14");
    await user.click(screen.getByRole("button", { name: "Save variant" }));

    await waitFor(() => expect(sent).toBeDefined());
    expect(sent).toMatchObject({ piecesMin: 10, piecesMax: 14, serves: 4 });
  });

  describe("photos", () => {
    const photos = [
      {
        id: "image-1",
        storageKey: "products/p/image-1",
        url: "/media/products/p/image-1/640.webp",
        altText: "Seer fish, whole on crushed ice",
        width: 1280,
        height: 1280,
        sortOrder: 0,
      },
      {
        id: "image-2",
        storageKey: "products/p/image-2",
        url: "/media/products/p/image-2/640.webp",
        altText: "Seer fish steaks",
        width: 1280,
        height: 1280,
        sortOrder: 1,
      },
    ];

    it("makes a later photo the main photo through the image order with If-Match", async () => {
      catalogHandlers();
      let ifMatch: string | null = null;
      let sent: unknown;
      server.use(
        http.get(`${API}/admin/products/${PRODUCT_ID}`, () =>
          HttpResponse.json(product({ images: photos })),
        ),
        http.put(`${API}/admin/products/${PRODUCT_ID}/images/order`, async ({ request }) => {
          ifMatch = request.headers.get("If-Match");
          sent = await request.json();
          return HttpResponse.json(product({ images: photos, version: "42" }));
        }),
      );
      const { user } = renderApp({
        path: `/products/${PRODUCT_ID}`,
        permissions: ["catalog.manage"],
      });

      await user.click(await screen.findByRole("tab", { name: /Photos \(2\)/ }));
      expect(screen.getByRole("button", { name: "Make photo 1 the main photo" })).toBeDisabled();
      await user.click(screen.getByRole("button", { name: "Make photo 2 the main photo" }));

      await waitFor(() => expect(sent).toEqual({ imageIds: ["image-2", "image-1"] }));
      expect(ifMatch).toBe('"41"');
    });

    it("uploads a photo with its alt text", async () => {
      catalogHandlers();
      let altText: string | null = null;
      let fileName: string | null = null;
      server.use(
        http.get(`${API}/admin/products/${PRODUCT_ID}`, () =>
          HttpResponse.json(product({ images: photos })),
        ),
        http.post(`${API}/admin/products/${PRODUCT_ID}/images`, async ({ request }) => {
          const form = await request.formData();
          const sentAlt = form.get("altText");
          const sentFile = form.get("file");
          altText = typeof sentAlt === "string" ? sentAlt : null;
          fileName = sentFile !== null && typeof sentFile !== "string" ? sentFile.name : null;
          return HttpResponse.json(photos[0], { status: 201 });
        }),
      );
      const { user } = renderApp({
        path: `/products/${PRODUCT_ID}`,
        permissions: ["catalog.manage"],
      });

      await user.click(await screen.findByRole("tab", { name: /Photos/ }));
      await user.upload(
        screen.getByLabelText(/^Photo/, { selector: "input" }),
        new File([new Uint8Array([1, 2, 3])], "seer-3.webp", { type: "image/webp" }),
      );
      await user.type(
        screen.getByLabelText(/Alt text/, { selector: "input" }),
        "Andhra seer fish fry",
      );
      await user.click(screen.getByRole("button", { name: "Upload" }));

      await waitFor(() => expect(altText).toBe("Andhra seer fish fry"));
      expect(fileName).toBe("seer-3.webp");
    });

    it("deletes a photo with If-Match after confirming", async () => {
      catalogHandlers();
      let deleted: string | null = null;
      server.use(
        http.get(`${API}/admin/products/${PRODUCT_ID}`, () =>
          HttpResponse.json(product({ images: photos })),
        ),
        http.delete(
          `${API}/admin/products/${PRODUCT_ID}/images/:imageId`,
          ({ params, request }) => {
            deleted = `${String(params.imageId)} ${request.headers.get("If-Match") ?? ""}`;
            return HttpResponse.json(product({ images: [photos[0]!], version: "42" }));
          },
        ),
      );
      const { user } = renderApp({
        path: `/products/${PRODUCT_ID}`,
        permissions: ["catalog.manage"],
      });

      await user.click(await screen.findByRole("tab", { name: /Photos/ }));
      await user.click(screen.getByRole("button", { name: "Delete photo 2" }));
      await user.click(await screen.findByRole("button", { name: "Delete photo" }));

      await waitFor(() => expect(deleted).toBe('image-2 "41"'));
    });
  });

  it("refuses the editor without catalog.manage", async () => {
    renderApp({ path: `/products/${PRODUCT_ID}`, permissions: ["orders.view"] });
    expect(await screen.findByText("You do not have access to this page")).toBeInTheDocument();
  });
});
