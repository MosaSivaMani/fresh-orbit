import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HomeCategories } from "./home-categories";

const categories = [
  { slug: "sea-fish", name: "Sea fish", imageUrl: "/media/categories/sea-fish.webp" },
  { slug: "prawns", name: "Prawns", imageUrl: null },
];

describe("HomeCategories", () => {
  it("links one circle per server category under the section heading", () => {
    render(<HomeCategories categories={categories} />);

    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("What's on your mind?");
    const nav = screen.getByRole("navigation", { name: "Categories" });
    const links = within(nav).getAllByRole("link");
    expect(links).toHaveLength(2);
    expect(within(nav).getByRole("link", { name: "Sea fish" })).toHaveAttribute(
      "href",
      "/shop/sea-fish",
    );
    expect(within(nav).getByRole("link", { name: "Prawns" })).toHaveAttribute(
      "href",
      "/shop/prawns",
    );
  });

  it("uses the category picture when the server has one and the fish mark otherwise", () => {
    render(<HomeCategories categories={categories} />);

    const nav = screen.getByRole("navigation", { name: "Categories" });
    const withPicture = within(nav).getByRole("link", { name: "Sea fish" });
    expect(withPicture.querySelector("img")).not.toBeNull();
    const withoutPicture = within(nav).getByRole("link", { name: "Prawns" });
    expect(withoutPicture.querySelector("img")).toBeNull();
    expect(withoutPicture.querySelector("svg")).not.toBeNull();
  });

  it("offers View all to the shop", () => {
    render(<HomeCategories categories={categories} />);

    expect(screen.getByRole("link", { name: "View all" })).toHaveAttribute("href", "/shop");
  });

  it("renders nothing when the server features no categories", () => {
    const { container } = render(<HomeCategories categories={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});
