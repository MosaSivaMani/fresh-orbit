import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import {
  CategoryTile,
  CategoryTileRow,
  CheckBar,
  FeatureChip,
  HarbourHero,
  HeroHeadline,
  PillLink,
  PromoCard,
  PromoCardGrid,
  SectionHeader,
} from "./home-rich";

describe("HarbourHero", () => {
  it("shows the headline, features, tagline and picture; drawings are hidden", async () => {
    const { container } = render(
      <HarbourHero
        aria-labelledby="hero"
        heading={<HeroHeadline id="hero" script="Fresh fish." brush="Cleaned." line="At door." />}
        featuresLabel="Why us"
        features={
          <>
            <FeatureChip icon={<svg />} title="CLEANED" line="Your way" detail="Whole" />
            <FeatureChip icon={<svg />} title="PAY FOR" line="Net weight" />
          </>
        }
        tagline="From the boat"
        picture={<img src="/p.png" alt="Padma holding a fish" />}
      />,
    );

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "Fresh fish. Cleaned. At door.",
    );
    const features = screen.getByRole("list", { name: "Why us" });
    expect(within(features).getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("From the boat")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Padma holding a fish" })).toBeInTheDocument();
    for (const svg of Array.from(container.querySelectorAll("svg"))) {
      expect(svg.closest("[aria-hidden='true']")).not.toBeNull();
    }
    await expectNoAxeViolations(container);
  });

  it("puts a background picture behind the hero as decoration", () => {
    const { container } = render(
      <HarbourHero heading={<h1>Hi</h1>} background={<img src="/bg.jpg" alt="" />} />,
    );

    expect(container.querySelector("img")?.closest("[aria-hidden='true']")).not.toBeNull();
  });
});

describe("CheckBar", () => {
  it("renders both parts", () => {
    render(<CheckBar start={<p>Pincode</p>} end={<p>Search</p>} />);

    expect(screen.getByText("Pincode")).toBeInTheDocument();
    expect(screen.getByText("Search")).toBeInTheDocument();
  });
});

describe("SectionHeader", () => {
  it("names the heading with the handwritten word and shows the action", () => {
    render(
      <SectionHeader
        headingId="cats"
        title="What's on your"
        script="mind?"
        action={<PillLink href="/shop">View all</PillLink>}
      />,
    );

    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("What's on your mind?");
    expect(screen.getByRole("link", { name: "View all" })).toHaveAttribute("href", "/shop");
  });
});

describe("CategoryTile", () => {
  it("is a link named by its label inside a named list", async () => {
    const { container } = render(
      <CategoryTileRow label="Categories">
        <CategoryTile href="/shop/a" label="Sea fish" picture={<img src="/a.png" alt="" />} />
        <CategoryTile href="/shop/b" label="Prawns" />
      </CategoryTileRow>,
    );

    const nav = screen.getByRole("navigation", { name: "Categories" });
    expect(within(nav).getAllByRole("listitem")).toHaveLength(2);
    expect(within(nav).getByRole("link", { name: "Sea fish" })).toHaveAttribute("href", "/shop/a");
    await expectNoAxeViolations(container);
  });
});

describe("PromoCard", () => {
  it("shows the title as a heading, the line, the picture and the action", async () => {
    const { container } = render(
      <PromoCardGrid>
        <PromoCard
          title="Cleaned Your Way"
          line="Whole · Steaks"
          picture={<img src="/c.png" alt="Fish steaks" />}
          action={<PillLink tone="coral" href="/support">Learn more</PillLink>}
        />
      </PromoCardGrid>,
    );

    expect(screen.getAllByRole("listitem")).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 3 })).toHaveTextContent("Cleaned Your Way");
    expect(screen.getByText("Whole · Steaks")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Fish steaks" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Learn more" })).toHaveAttribute("href", "/support");
    await expectNoAxeViolations(container);
  });
});
