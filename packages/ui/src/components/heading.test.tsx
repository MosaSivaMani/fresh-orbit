import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { ColourBlockScreen } from "./bold-blocks";
import { Brush, Heading, Script } from "./heading";

describe("Heading", () => {
  it("stacks two lines and makes the second one handwritten by default", async () => {
    render(<Heading level={1} line1="Cleaned" line2="your way" doodle />);
    const h = screen.getByRole("heading", { level: 1, name: /^Cleaned\s?your way$/ });
    expect(h.querySelector("[data-script]")).toHaveTextContent("your way");
    expect(h.querySelector("[data-brush]")).toBeNull();
    expect(h).toHaveClass("fo-doodle");
    await expectNoAxeViolations();
  });

  it("puts the handwritten line and the brush where asked", () => {
    render(<Heading line1="Straight off" line2="the boats" script="line1" brush="line2" />);
    const h = screen.getByRole("heading", { level: 2 });
    expect(h.querySelector("[data-script]")).toHaveTextContent("Straight off");
    expect(h.querySelector("[data-brush]")).toHaveTextContent("the boats");
  });

  it("has no handwritten line for a single line unless asked", () => {
    render(<Heading line1="Bill details" />);
    expect(screen.getByRole("heading").querySelector("[data-script]")).toBeNull();
  });

  it("accepts inline content with Script and Brush", () => {
    render(
      <Heading level={3}>
        What&apos;s cooking <Script tone="accent">today?</Script> <Brush tone="paper">now</Brush>
      </Heading>,
    );
    const h = screen.getByRole("heading", { level: 3 });
    expect(h).toHaveTextContent("What's cooking today? now");
    expect(h.querySelector("[data-brush='paper']")).toHaveTextContent("now");
  });
});

describe("ColourBlockScreen title lines", () => {
  it("uses a paper brush on mango and keeps the full title as the region name (browsers add the space between block lines)", () => {
    render(
      <ColourBlockScreen
        tone="mango"
        title="At your door"
        title2="today"
        titleBrush="line2"
        slot="ONBOARDING_DELIVERY"
      />,
    );
    const region = screen.getByRole("region", { name: /^At your door\s?today$/ });
    expect(region.querySelector("[data-brush='paper']")).toHaveTextContent("today");
    expect(region.querySelector("h1")).toHaveClass("fo-doodle");
  });
});
