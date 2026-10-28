import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { type HomeBanner } from "@/api/types/catalog";
import { OfferStrip } from "./offer-strip";
import { offersBySlug } from "./product-card";

const banner: HomeBanner = {
  id: "b1",
  title: "₹90 off Seer Fish",
  subtitle: null,
  imageKey: null,
  imageUrl: null,
  linkType: "PRODUCT",
  linkTarget: "0a7c2d4e-0000-4000-8000-000000000001",
  linkSlug: "seer-fish-vanjaram",
  startsAt: "2026-10-01T00:00:00Z",
  endsAt: null,
  sortOrder: 0,
};

describe("OfferStrip", () => {
  it("links a product banner by the server's slug, never by the id", () => {
    render(<OfferStrip banners={[banner]} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/products/seer-fish-vanjaram");
  });

  it("gives a product banner without a slug no link", () => {
    render(<OfferStrip banners={[{ ...banner, linkSlug: null }]} />);
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("links a category banner by the server's slug, else the featured categories' map", () => {
    const category = { ...banner, linkType: "CATEGORY" as const, linkTarget: "c1" };
    const { unmount } = render(<OfferStrip banners={[{ ...category, linkSlug: "prawns" }]} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/shop/prawns");
    unmount();

    const unlinked = { ...category, linkSlug: null };
    render(<OfferStrip banners={[unlinked]} categorySlugs={{ c1: "crab" }} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/shop/crab");
  });
});

describe("OfferStrip pictures (W11)", () => {
  it("shows the server's resolved picture and badge", () => {
    const { container } = render(
      <OfferStrip
        banners={[
          {
            ...banner,
            imageKey: "sample/banners/welcome.webp",
            imageUrl: "/media/sample/banners/welcome.webp",
            badge: "New",
          },
        ]}
      />,
    );
    const src = container.querySelector("img")?.getAttribute("src") ?? "";
    expect(decodeURIComponent(src)).toContain("/media/sample/banners/welcome.webp");
    expect(screen.getByText("New")).toBeInTheDocument();
  });

  it("shows no picture when the server resolved none, even with a storage key", () => {
    const { container } = render(
      <OfferStrip banners={[{ ...banner, imageKey: "sample/banners/welcome.webp" }]} />,
    );
    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByText("Offer")).toBeInTheDocument();
  });
});

describe("offersBySlug", () => {
  it("keys product offers by the server's slug and skips banners without one", () => {
    const offers = offersBySlug([
      banner,
      { ...banner, id: "b2", title: "Second offer" },
      { ...banner, id: "b3", title: "No slug", linkSlug: null },
    ]);
    expect(offers).toEqual({ "seer-fish-vanjaram": "₹90 off Seer Fish" });
  });
});
