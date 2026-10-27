import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import {
  CompactProductCard,
  Highlight,
  OfferRow,
  SectionAction,
  SectionHeader,
  SkyPanel,
  TextPageShell,
  ThemeBand,
} from "./theme";

describe("ThemeBand", () => {
  it("renders the page heading with the highlighted word and the handwritten line", async () => {
    render(
      <ThemeBand
        eyebrow="Shop"
        title={
          <>
            Fresh <Highlight>catch</Highlight>
          </>
        }
        script="Straight off the boats"
        description="Cleaned your way."
      />,
    );
    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent("Fresh catch");
    expect(heading.querySelector("[data-brush]")).toHaveTextContent("catch");
    expect(screen.getByText("Straight off the boats")).toBeInTheDocument();
    await expectNoAxeViolations();
  });

  it("uses a second-level heading when asked", () => {
    render(<ThemeBand title="Your cart" headingLevel={2} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Your cart");
    expect(screen.queryByRole("heading", { level: 1 })).toBeNull();
  });
});

describe("TextPageShell", () => {
  it("puts the text on a card and the side column beside it", () => {
    render(
      <TextPageShell title="Refund policy" side={<nav aria-label="Other policies" />}>
        <h2>Refunds</h2>
      </TextPageShell>,
    );
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Refund policy");
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Refunds");
    expect(screen.getByRole("navigation", { name: "Other policies" })).toBeInTheDocument();
  });
});

describe("SectionHeader", () => {
  it("labels the heading by id and shows the action link", async () => {
    render(
      <section aria-labelledby="best">
        <SectionHeader
          id="best"
          title="Best"
          script="sellers"
          action={<SectionAction href="/shop">View all</SectionAction>}
        />
      </section>,
    );
    expect(screen.getByRole("region", { name: /Best/ })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2 })).toHaveAttribute("id", "best");
    expect(screen.getByRole("link", { name: "View all" })).toHaveAttribute("href", "/shop");
    await expectNoAxeViolations();
  });
});

describe("CompactProductCard", () => {
  it("is one link with the name, the price line and the note", async () => {
    render(
      <CompactProductCard
        href="/products/seer"
        picture={<img src="/seer.jpg" alt="" />}
        name="Seer Fish"
        price="₹669.50 / kg"
        note="Sold out for now"
      />,
    );
    const link = screen.getByRole("link", { name: /Seer Fish/ });
    expect(link).toHaveAttribute("href", "/products/seer");
    expect(within(link).getByText("₹669.50 / kg")).toBeInTheDocument();
    expect(within(link).getByText("Sold out for now")).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});

describe("OfferRow", () => {
  it("shows the code, the terms and the action", () => {
    render(
      <ul aria-label="Available offers">
        <OfferRow
          code="FISH50"
          description="₹50 off"
          terms="Orders from ₹500."
          action={<button type="button">Apply</button>}
        />
      </ul>,
    );
    const offers = screen.getByRole("list", { name: "Available offers" });
    expect(within(offers).getByRole("listitem")).toHaveTextContent("FISH50");
    expect(within(offers).getByRole("button", { name: "Apply" })).toBeInTheDocument();
  });
});

describe("SkyPanel", () => {
  it("passes its role through", () => {
    render(
      <SkyPanel role="alert">
        <p>Something went wrong</p>
      </SkyPanel>,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Something went wrong");
  });
});
