import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { AppShell } from "./app-shell";
import { Button } from "./button";
import { PriceTag, QuantityStepper, WeightLabel } from "./commerce";
import { EmptyState, ErrorState, StatusBadge } from "./display";
import {
  Dialog,
  DialogContent,
  DialogTrigger,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "./overlay";
import { Pagination, paginationRange } from "./pagination";
import { Toaster, toast } from "./toast";

describe("StatusBadge", () => {
  const map = { OUT_FOR_DELIVERY: { label: "Out for delivery", tone: "info" } } as const;

  it("uses the map label and tone", () => {
    render(<StatusBadge code="OUT_FOR_DELIVERY" map={map} />);
    const badge = screen.getByText("Out for delivery").closest("[data-status]");
    expect(badge).toHaveAttribute("data-status", "OUT_FOR_DELIVERY");
    expect(badge?.className).toContain("bg-info-subtle");
  });

  it("humanises unknown codes", () => {
    render(<StatusBadge code="PENDING_PAYMENT" map={map} />);
    expect(screen.getByText("Pending payment")).toBeInTheDocument();
  });
});

describe("commerce display", () => {
  it("PriceTag formats paise and announces the original price", () => {
    render(<PriceTag paise={64900} mrpPaise={74900} per="/ kg" />);
    expect(screen.getByText(/₹649/)).toBeInTheDocument();
    const mrp = screen.getByText(/₹749/);
    expect(mrp.tagName).toBe("S");
    expect(mrp).toHaveTextContent("Original price ₹749");
  });

  it("PriceTag hides an MRP that is not higher", () => {
    render(<PriceTag paise={64900} mrpPaise={64900} />);
    expect(screen.queryByText(/Original price/)).not.toBeInTheDocument();
  });

  it("WeightLabel shows gross and net range", () => {
    render(<WeightLabel grams={1000} netMinGrams={700} netMaxGrams={800} prefix="Gross" />);
    expect(screen.getByText("Gross 1 kg")).toBeInTheDocument();
    expect(screen.getByText(/Net 700–800 g/)).toBeInTheDocument();
  });

  it("QuantityStepper steps within bounds and labels its buttons", async () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <QuantityStepper
        label="Seer fish"
        value={500}
        min={250}
        max={750}
        step={250}
        onChange={onChange}
        formatValue={(v) => `${v} g`}
      />,
    );
    const group = screen.getByRole("group", { name: "Quantity of Seer fish" });
    await userEvent.click(
      within(group).getByRole("button", { name: "Increase quantity of Seer fish" }),
    );
    expect(onChange).toHaveBeenLastCalledWith(750);
    expect(screen.getByText("500 g")).toBeInTheDocument();

    rerender(
      <QuantityStepper
        label="Seer fish"
        value={750}
        min={250}
        max={750}
        step={250}
        onChange={onChange}
      />,
    );
    expect(screen.getByRole("button", { name: "Increase quantity of Seer fish" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Decrease quantity of Seer fish" })).toBeEnabled();
    await expectNoAxeViolations();
  });
});

describe("states", () => {
  it("ErrorState is an alert with trace id and retry", async () => {
    const onRetry = vi.fn();
    render(<ErrorState traceId="00-abc-01" onRetry={onRetry} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Reference: 00-abc-01");
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetry).toHaveBeenCalled();
  });

  it("EmptyState renders title and action", async () => {
    render(<EmptyState title="Your cart is empty" action={<Button>Browse fish</Button>} />);
    expect(screen.getByText("Your cart is empty")).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});

describe("Pagination", () => {
  it("computes a stable range with ellipses", () => {
    expect(paginationRange(1, 5)).toEqual([1, 2, 3, 4, 5]);
    expect(paginationRange(1, 20)).toEqual([1, 2, 3, 4, 5, "ellipsis-end", 20]);
    expect(paginationRange(10, 20)).toEqual([1, "ellipsis-start", 9, 10, 11, "ellipsis-end", 20]);
    expect(paginationRange(20, 20)).toEqual([1, "ellipsis-start", 16, 17, 18, 19, 20]);
  });

  it("marks the current page and navigates", async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={2} pageSize={20} totalCount={134} onPageChange={onPageChange} />);
    const nav = screen.getByRole("navigation", { name: "Pagination" });
    expect(nav).toHaveTextContent("Showing 21–40 of 134");
    expect(screen.getByRole("button", { name: "Page 2" })).toHaveAttribute("aria-current", "page");
    await userEvent.click(screen.getByRole("button", { name: "Next page" }));
    expect(onPageChange).toHaveBeenCalledWith(3);
    await expectNoAxeViolations();
  });

  it("disables previous on the first page", () => {
    render(<Pagination page={1} pageSize={20} totalCount={10} onPageChange={() => {}} />);
    expect(screen.getByRole("button", { name: "Previous page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next page" })).toBeDisabled();
  });
});

describe("Dialog and Tabs", () => {
  it("opens a named dialog, moves focus inside and closes with Escape", async () => {
    render(
      <Dialog>
        <DialogTrigger asChild>
          <Button>Cancel order</Button>
        </DialogTrigger>
        <DialogContent title="Cancel order FO-260001?" description="This cannot be undone.">
          <p>Body</p>
        </DialogContent>
      </Dialog>,
    );
    await userEvent.click(screen.getByRole("button", { name: "Cancel order" }));
    const dialog = screen.getByRole("dialog", { name: "Cancel order FO-260001?" });
    expect(dialog).toHaveAccessibleDescription("This cannot be undone.");
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
    await expectNoAxeViolations(dialog);
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("Tabs switch with arrow keys", async () => {
    render(
      <Tabs defaultValue="a">
        <TabsList aria-label="Order sections">
          <TabsTrigger value="a">Items</TabsTrigger>
          <TabsTrigger value="b">Timeline</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Items panel</TabsContent>
        <TabsContent value="b">Timeline panel</TabsContent>
      </Tabs>,
    );
    await userEvent.click(screen.getByRole("tab", { name: "Items" }));
    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByRole("tab", { name: "Timeline" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel")).toHaveTextContent("Timeline panel");
  });
});

describe("Toast", () => {
  it("shows a queued toast", async () => {
    render(<Toaster />);
    act(() => {
      toast({ title: "Stock received", description: "12 kg seer fish added.", tone: "success" });
    });
    expect(await screen.findByText("Stock received")).toBeInTheDocument();
    expect(screen.getByText("12 kg seer fish added.")).toBeInTheDocument();
  });
});

describe("AppShell", () => {
  const navigation = [
    {
      id: "main",
      items: [
        { id: "dashboard", label: "Dashboard", href: "/", active: true },
        { id: "orders", label: "Orders", href: "/orders" },
      ],
    },
  ];

  it("has landmarks, a skip link, current page and a collapsible sidebar", async () => {
    const onCollapsedChange = vi.fn();
    render(
      <AppShell brand="Fresh Orbit" navigation={navigation} onCollapsedChange={onCollapsedChange}>
        <h1>Dashboard</h1>
      </AppShell>,
    );
    expect(screen.getByRole("main")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Skip to content" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Dashboard" })).toHaveAttribute("aria-current", "page");

    const toggle = screen.getByRole("button", { name: "Collapse sidebar" });
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    await userEvent.click(toggle);
    expect(onCollapsedChange).toHaveBeenCalledWith(true);
    expect(screen.getByRole("button", { name: "Expand sidebar" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    await expectNoAxeViolations();
  });

  it("opens the mobile navigation drawer", async () => {
    render(
      <AppShell brand="Fresh Orbit" navigation={navigation}>
        content
      </AppShell>,
    );
    await userEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    const drawer = screen.getByRole("dialog", { name: "Fresh Orbit" });
    expect(within(drawer).getByRole("link", { name: "Orders" })).toBeInTheDocument();
  });
});
