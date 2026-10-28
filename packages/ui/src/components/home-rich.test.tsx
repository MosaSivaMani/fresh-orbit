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
  WaveBand,
} from "./home-rich";

describe("HarbourHero", () => {
  it("shows the headline, features, tagline and picture; drawings are hidden", async () => {
    const heading = <HeroHeadline id="hero" script="Fresh fish." brush="Cleaned." line="Door." />;
    const { container } = render(
      <HarbourHero
        aria-labelledby="hero"
        heading={heading}
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
      "Fresh fish. Cleaned. Door.",
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

  it("sets the handwritten note's line breaks from an array", () => {
    render(<HarbourHero heading={<h1>Hi</h1>} tagline={["From", "the boat"]} />);

    expect(screen.getByText("From")).toBeInTheDocument();
    expect(screen.getByText("the boat")).toBeInTheDocument();
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
    const action = (
      <PillLink tone="coral" href="/support">
        Learn more
      </PillLink>
    );
    const picture = <img src="/c.png" alt="Fish steaks" />;
    const { container } = render(
      <PromoCardGrid>
        <PromoCard title="Cleaned Your Way" line="Whole" picture={picture} action={action} />
      </PromoCardGrid>,
    );

    expect(screen.getAllByRole("listitem")).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 3 })).toHaveTextContent("Cleaned Your Way");
    expect(screen.getByText("Whole")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Fish steaks" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Learn more" })).toHaveAttribute("href", "/support");
    await expectNoAxeViolations(container);
  });
});

describe("WaveBand", () => {
  it("shows its content over a hidden wave", () => {
    const { container } = render(
      <WaveBand>
        <p>Promos</p>
      </WaveBand>,
    );

    expect(screen.getByText("Promos")).toBeInTheDocument();
    expect(container.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
  });
});
