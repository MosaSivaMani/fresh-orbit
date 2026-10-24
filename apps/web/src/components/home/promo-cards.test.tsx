import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PromoCards, homePromoCards } from "./promo-cards";

describe("PromoCards", () => {
  it("shows the three store promises with their links", () => {
    render(<PromoCards />);

    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(3);
    const [catchCard, cleanedCard, deliveredCard] = items;

    expect(within(catchCard!).getByRole("heading", { level: 3 })).toHaveTextContent(
      "Today's Fresh Catch",
    );
    expect(within(catchCard!).getByRole("link", { name: "Shop fresh fish" })).toHaveAttribute(
      "href",
      "/shop",
    );
    expect(within(cleanedCard!).getByRole("heading", { level: 3 })).toHaveTextContent(
      "Cleaned Your Way",
    );
    expect(within(cleanedCard!).getByRole("link", { name: "Learn more" })).toHaveAttribute(
      "href",
      "/support",
    );
    expect(within(deliveredCard!).getByRole("heading", { level: 3 })).toHaveTextContent(
      "Chilled & Delivered Fresh",
    );
    expect(
      within(deliveredCard!).getByRole("link", { name: "See how it works" }),
    ).toHaveAttribute("href", "/support/delivery-policy");
  });

  it("gives every picture alt text and the section a heading", () => {
    render(<PromoCards />);

    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    const images = screen.getAllByRole("img");
    expect(images).toHaveLength(homePromoCards.length);
    for (const image of images) expect(image).toHaveAccessibleName();
  });
});
