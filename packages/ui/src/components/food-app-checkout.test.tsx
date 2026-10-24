import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { BillBox, StatusSteps, StepCard, StickyActionBar } from "./food-app-checkout";

describe("StepCard", () => {
  it("is a section named by its title, with the step number read out", async () => {
    const { container } = render(
      <StepCard step={2} title="Delivery slot" action={<button type="button">Change</button>}>
        <p>Pick a time</p>
      </StepCard>,
    );
    const section = screen.getByRole("region", { name: "Step 2: Delivery slot" });
    expect(within(section).getByRole("heading", { level: 2 })).toHaveTextContent("Delivery slot");
    expect(within(section).getByRole("button", { name: "Change" })).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });
});

describe("BillBox", () => {
  it("shows the rows and the total exactly as given, without adding them up", async () => {
    const { container } = render(
      <BillBox
        rows={[
          { label: "Items", value: "₹649.50" },
          { label: "Coupon FISH50", value: "− ₹50.00", tone: "saving" },
          { label: "Delivery fee", value: "Free" },
        ]}
        total={{ label: "To pay", value: "₹1.00" }}
        notes={<p>You pay at most ₹2.00.</p>}
      />,
    );
    const bill = screen.getByRole("region", { name: "Bill details" });
    expect(within(bill).getByText("₹649.50")).toBeInTheDocument();
    expect(within(bill).getByText("− ₹50.00")).toHaveClass("tabular-nums");
    // The total is the value passed in (deliberately not the sum of the rows).
    expect(within(bill).getByText("₹1.00")).toBeInTheDocument();
    expect(within(bill).getByText("You pay at most ₹2.00.")).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });
});

describe("StickyActionBar", () => {
  it("is a named region with the amount and the action", () => {
    render(
      <StickyActionBar
        summary="Estimated total"
        amount="₹669.50"
        action={<button type="button">Place order</button>}
      />,
    );
    const bar = screen.getByRole("region", { name: "Order total" });
    expect(within(bar).getByText("₹669.50")).toBeInTheDocument();
    expect(within(bar).getByRole("button", { name: "Place order" })).toBeInTheDocument();
  });
});

describe("StatusSteps", () => {
  it("marks steps done, the current one with aria-current and the rest upcoming", async () => {
    const { container } = render(
      <StatusSteps
        steps={[
          { key: "CONFIRMED", label: "Confirmed", time: "10:41 am", state: "done" },
          { key: "PACKED", label: "Packed", state: "current" },
          { key: "DELIVERED", label: "Delivered", state: "upcoming" },
        ]}
      />,
    );
    const list = screen.getByRole("list", { name: "Order progress" });
    const items = within(list).getAllByRole("listitem");
    expect(items.map((li) => li.dataset.state)).toEqual(["done", "current", "upcoming"]);
    expect(items[1]).toHaveAttribute("aria-current", "step");
    expect(items[0]).not.toHaveAttribute("aria-current");
    expect(items[0]).toHaveTextContent("Confirmed (done)10:41 am");
    await expectNoAxeViolations(container);
  });
});
