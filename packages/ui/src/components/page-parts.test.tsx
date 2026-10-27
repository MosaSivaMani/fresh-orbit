import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import {
  BlockSplitCard,
  DetailList,
  PageHeader,
  SectionCard,
  StatTile,
  Wordmark,
} from "./page-parts";

describe("PageHeader", () => {
  it("renders the title as the page's h1 with the doodle, the back slot, meta and actions", async () => {
    render(
      <PageHeader
        title="Orders"
        description="Orders placed today"
        back={<a href="/">Back</a>}
        meta={<span>LIVE</span>}
        actions={<button type="button">New order</button>}
      />,
    );
    const h1 = screen.getByRole("heading", { level: 1, name: "Orders" });
    expect(h1).toHaveClass("fo-doodle");
    expect(screen.getByRole("link", { name: "Back" })).toBeInTheDocument();
    expect(screen.getByText("LIVE")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New order" })).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});

describe("SectionCard and DetailList", () => {
  it("titles the section at the given level and shows a dash for a missing value", () => {
    render(
      <SectionCard title="Customer" headingLevel={3}>
        <DetailList
          items={[
            { label: "Name", value: "Asha" },
            { label: "Email", value: null },
          ]}
        />
      </SectionCard>,
    );
    expect(screen.getByRole("heading", { level: 3, name: "Customer" })).toBeInTheDocument();
    expect(screen.getByText("Email").nextElementSibling).toHaveTextContent("—");
  });
});

describe("StatTile", () => {
  it("shows the figure on its block colour", () => {
    const { container } = render(<StatTile tone="teal" label="Orders today" value={12} sub="Cancelled: 1" />);
    expect(container.firstElementChild).toHaveAttribute("data-tone", "teal");
    expect(container.firstElementChild).toHaveTextContent("Orders today12Cancelled: 1");
  });

  it("colours the figure only on paper and hides it while loading", () => {
    const { rerender } = render(<StatTile label="Low stock" value={3} emphasis="warning" />);
    expect(screen.getByText("3")).toHaveClass("text-warning-text");
    rerender(<StatTile tone="mango" label="Low stock" value={3} emphasis="warning" />);
    expect(screen.getByText("3")).not.toHaveClass("text-warning-text");
    rerender(<StatTile label="Low stock" value={3} loading />);
    expect(screen.queryByText("3")).toBeNull();
  });
});

describe("Wordmark", () => {
  it("names the brand once, with the mark decorative, also when compact", () => {
    const { rerender } = render(<Wordmark markSrc="/brand/mark.png" />);
    expect(screen.getByText("Orbitt")).toHaveAttribute("data-script");
    expect(document.querySelector("img")).toHaveAttribute("alt", "");
    rerender(<Wordmark markSrc="/brand/mark.png" compact />);
    expect(screen.getByText("Fresh Orbitt")).toHaveClass("sr-only");
  });
});

describe("BlockSplitCard", () => {
  it("puts the aside on the colour block and the children on paper", () => {
    render(
      <BlockSplitCard tone="mango" aside={<p>Fresh today</p>}>
        <h1>Sign in</h1>
      </BlockSplitCard>,
    );
    expect(screen.getByText("Fresh today").parentElement).toHaveClass("bg-block-mango");
    expect(screen.getByRole("heading", { name: "Sign in" })).toBeInTheDocument();
  });
});
