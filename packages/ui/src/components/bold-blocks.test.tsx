import { illustrations } from "@fresh/illustrations";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import {
  AvatarCircle,
  BentoTile,
  CategoryBubble,
  ColourBlockScreen,
  CouponStrip,
  FilterChip,
  FloatingCartBar,
  OptionTile,
  ProductCard,
  TabBar,
} from "./bold-blocks";
import { Button } from "./button";
import { QuantityStepper } from "./commerce";

describe("AvatarCircle", () => {
  it("shows the slot's picture cropped in a circle, decorative by default", () => {
    const { container } = render(<AvatarCircle slot="EMPTY_CART" size="sm" focus="40% 20%" />);
    const img = container.querySelector("img");
    expect(img).toHaveAttribute("alt", "");
    expect(img).toHaveAttribute("data-slot", "EMPTY_CART");
    expect(img).toHaveStyle({ objectPosition: "40% 20%" });
    expect(container.firstElementChild).toHaveAttribute("data-avatar-size", "sm");
  });

  it("keeps the manifest alt text when it is content", () => {
    render(<AvatarCircle slot="EMPTY_CART" decorative={false} />);
    expect(screen.getByRole("img", { name: illustrations.EMPTY_CART.alt })).toBeInTheDocument();
  });

  it("has no orbit halo or other moving parts", () => {
    const { container } = render(<AvatarCircle slot="EMPTY_CART" />);
    expect(container.innerHTML).not.toMatch(/animate-/);
  });
});

describe("ColourBlockScreen", () => {
  it("is a section named by its title, with the sheet heading one level down", async () => {
    render(
      <ColourBlockScreen
        tone="coral"
        eyebrow="Your cart"
        title="Basket's empty"
        slot="EMPTY_CART"
        sheetTitle="Nothing in here yet"
        stickers={[{ label: "Fresh today", side: "right" }]}
      >
        <p>Seer, pomfret and prawns came in this morning.</p>
        <Button>See today's catch</Button>
      </ColourBlockScreen>,
    );
    const region = screen.getByRole("region", { name: "Basket's empty" });
    expect(region).toHaveAttribute("data-tone", "coral");
    expect(within(region).getByRole("heading", { level: 1 })).toHaveTextContent("Basket's empty");
    expect(within(region).getByRole("heading", { level: 2 })).toHaveTextContent(
      "Nothing in here yet",
    );
    expect(within(region).getByText("Fresh today")).toBeInTheDocument();
    expect(within(region).getByText("Your cart")).toBeInTheDocument();
    await expectNoAxeViolations();
  });

  it("accepts another heading level", () => {
    render(<ColourBlockScreen tone="ice" title="All quiet" slot="OFFLINE" headingLevel={2} />);
    expect(screen.getByRole("heading", { level: 2, name: "All quiet" })).toBeInTheDocument();
  });
});

describe("BentoTile", () => {
  it("renders as a link with its title, line and chip", async () => {
    render(
      <BentoTile
        asChild
        tone="teal"
        wide
        chip="Caught 5:40 am"
        title="Today's catch is in"
        description="Seer, pomfret"
        slot="HOME_HERO"
      >
        <a href="/shop" aria-label="Today's catch is in" />
      </BentoTile>,
    );
    const link = screen.getByRole("link", { name: "Today's catch is in" });
    expect(link).toHaveAttribute("href", "/shop");
    expect(within(link).getByText("Caught 5:40 am")).toBeInTheDocument();
    expect(within(link).getByText("Seer, pomfret")).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});

describe("CategoryBubble", () => {
  it("marks the current category", async () => {
    render(
      <nav aria-label="Categories">
        <CategoryBubble href="/shop/fish" label="Fish" selected />
        <CategoryBubble href="/shop/prawns" label="Prawns" tone="mango" />
      </nav>,
    );
    expect(screen.getByRole("link", { name: "Fish" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Prawns" })).not.toHaveAttribute("aria-current");
    await expectNoAxeViolations();
  });

  it("styles a router link with asChild", () => {
    render(
      <CategoryBubble asChild label="Crab">
        {/* eslint-disable-next-line jsx-a11y/anchor-has-content -- asChild fills the link */}
        <a href="/shop/crab" data-router="yes" />
      </CategoryBubble>,
    );
    const link = screen.getByRole("link", { name: "Crab" });
    expect(link).toHaveAttribute("data-router", "yes");
  });
});

describe("FilterChip", () => {
  it("is a toggle button", async () => {
    const onClick = vi.fn();
    render(
      <FilterChip pressed onClick={onClick}>
        Cleaned free
      </FilterChip>,
    );
    const chip = screen.getByRole("button", { name: "Cleaned free" });
    expect(chip).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(chip);
    expect(onClick).toHaveBeenCalledOnce();
    await expectNoAxeViolations();
  });
});

describe("ProductCard", () => {
  const base = {
    name: "Seer fish steaks",
    label: "Seer fish steaks",
    href: "/products/seer-fish",
    detail: "500 g · 6–8 pcs",
    price: <span>₹590</span>,
  };

  it("shows ADD when the item is not in the cart", async () => {
    const onAdd = vi.fn();
    render(
      <ProductCard {...base} onAdd={onAdd} onQuantityChange={vi.fn()} badge="Caught 5:40 am" />,
    );
    expect(screen.getByRole("link", { name: "Seer fish steaks" })).toHaveAttribute(
      "href",
      "/products/seer-fish",
    );
    expect(screen.getByText("₹590")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Add Seer fish steaks to cart" }));
    expect(onAdd).toHaveBeenCalledOnce();
    expect(screen.queryByRole("group")).toBeNull();
    await expectNoAxeViolations();
  });

  it("turns into a stepper once the server reports a quantity, and asks for 0 below the minimum", async () => {
    const onQuantityChange = vi.fn();
    render(
      <ProductCard
        {...base}
        quantity={1}
        min={1}
        max={5}
        onAdd={vi.fn()}
        onQuantityChange={onQuantityChange}
      />,
    );
    expect(screen.queryByRole("button", { name: /^Add / })).toBeNull();
    const group = screen.getByRole("group", { name: "Quantity of Seer fish steaks" });
    expect(within(group).getByText("1")).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("button", { name: "Increase quantity of Seer fish steaks" }),
    );
    expect(onQuantityChange).toHaveBeenLastCalledWith(2);
    await userEvent.click(
      screen.getByRole("button", { name: "Remove Seer fish steaks from cart" }),
    );
    expect(onQuantityChange).toHaveBeenLastCalledWith(0);
  });

  it("makes ADD a link to the product page when a choice is needed first", () => {
    render(<ProductCard {...base} addHref="/products/seer-fish#buy" />);
    expect(
      screen.getByRole("link", { name: "Choose options for Seer fish steaks" }),
    ).toHaveAttribute("href", "/products/seer-fish#buy");
  });

  it("shows the unavailable label instead of ADD", () => {
    render(<ProductCard {...base} onAdd={vi.fn()} unavailableLabel="Out of stock" />);
    expect(screen.getByText("Out of stock")).toBeInTheDocument();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("renders through a router link", () => {
    render(
      <ProductCard
        {...base}
        renderLink={({ href, className, children }) => (
          <a href={href} className={className} data-router="yes">
            {children}
          </a>
        )}
      />,
    );
    expect(screen.getByRole("link", { name: "Seer fish steaks" })).toHaveAttribute(
      "data-router",
      "yes",
    );
  });
});

describe("QuantityStepper (Bold blocks variants)", () => {
  it("solid variant without allowRemove keeps decrease disabled at the minimum", () => {
    render(<QuantityStepper variant="solid" value={1} min={1} onChange={vi.fn()} label="Crab" />);
    expect(screen.getByRole("button", { name: "Decrease quantity of Crab" })).toBeDisabled();
    expect(screen.getByRole("group")).toHaveAttribute("data-variant", "solid");
  });
});

describe("FloatingCartBar", () => {
  it("shows the server summary and total and links to the cart", async () => {
    render(<FloatingCartBar summary="2 items added" total="₹1,339" href="/cart" />);
    const region = screen.getByRole("region", { name: "Cart summary" });
    expect(within(region).getByText("2 items added")).toBeInTheDocument();
    expect(within(region).getByText("₹1,339")).toBeInTheDocument();
    expect(within(region).getByRole("link", { name: "View cart" })).toHaveAttribute(
      "href",
      "/cart",
    );
    await expectNoAxeViolations();
  });
});

describe("TabBar", () => {
  const items = [
    { href: "/", label: "Home", icon: <svg /> },
    { href: "/shop", label: "Shop", icon: <svg /> },
    { href: "/cart", label: "Cart", icon: <svg />, badge: 2 },
    { href: "/account/orders", label: "Orders", icon: <svg /> },
  ];

  it("marks the current tab and shows the cart count", async () => {
    render(<TabBar items={items} current="/shop" />);
    const nav = screen.getByRole("navigation", { name: "Main" });
    expect(within(nav).getByRole("link", { name: "Shop" })).toHaveAttribute("aria-current", "page");
    expect(within(nav).getByRole("link", { name: /^Cart\s?\(2 in cart\)$/ })).toHaveAttribute(
      "href",
      "/cart",
    );
    expect(within(nav).getByRole("link", { name: "Home" })).not.toHaveAttribute("aria-current");
    await expectNoAxeViolations();
  });

  it("hides a zero badge", () => {
    render(<TabBar items={items.map((i) => ({ ...i, badge: 0 }))} />);
    expect(screen.getByRole("link", { name: "Cart" })).toBeInTheDocument();
  });
});

describe("OptionTile and CouponStrip", () => {
  it("option tiles are radios in a group", async () => {
    const onChange = vi.fn();
    render(
      <fieldset>
        <legend>Choose your cut</legend>
        <OptionTile
          name="cut"
          value="WHOLE"
          label="Whole"
          description="uncleaned weight"
          onChange={onChange}
        />
        <OptionTile name="cut" value="STEAKS" label="Steaks" defaultChecked onChange={onChange} />
      </fieldset>,
    );
    expect(screen.getByRole("radio", { name: "Steaks" })).toBeChecked();
    await userEvent.click(screen.getByRole("radio", { name: "Whole" }));
    expect(screen.getByRole("radio", { name: "Whole" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Whole" })).toHaveAccessibleDescription(
      "uncleaned weight",
    );
    await expectNoAxeViolations();
  });

  it("coupon strip shows the given text", () => {
    render(<CouponStrip title="WELCOME100 applied" description="You save ₹100 on this order" />);
    expect(screen.getByText("WELCOME100 applied")).toBeInTheDocument();
    expect(screen.getByText("You save ₹100 on this order")).toBeInTheDocument();
  });
});
