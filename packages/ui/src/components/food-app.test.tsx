import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import {
  AppBand,
  CategoryCircle,
  CharacterState,
  FoodCard,
  FoodHero,
  PageBand,
  PromiseCard,
} from "./food-app";

describe("FoodHero", () => {
  it("shows its content with the two side photos decorative", async () => {
    const { container } = render(
      <FoodHero leftSlot="ONBOARDING_CLEANED" rightSlot="ONBOARDING_DELIVERY">
        <h1>Fresh fish</h1>
        <PromiseCard title="FISH" line="FROM THE BOATS" label="CAUGHT TODAY" />
      </FoodHero>,
    );
    expect(screen.getByRole("heading", { level: 1, name: "Fresh fish" })).toBeInTheDocument();
    const imgs = container.querySelectorAll("img");
    expect(imgs).toHaveLength(2);
    imgs.forEach((img) => expect(img).toHaveAttribute("alt", ""));
    expect(screen.getByText("CAUGHT TODAY")).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });
});

describe("CategoryCircle", () => {
  it("is a link named by its label, marked current when selected", () => {
    render(
      <>
        <CategoryCircle href="/shop/fish" label="Fish" />
        <CategoryCircle href="/shop/prawns" label="Prawns" selected />
      </>,
    );
    expect(screen.getByRole("link", { name: "Fish" })).not.toHaveAttribute("aria-current");
    expect(screen.getByRole("link", { name: "Prawns" })).toHaveAttribute("aria-current", "page");
  });
});

describe("FoodCard", () => {
  const base = {
    name: "Seer fish steaks",
    label: "Seer fish steaks",
    href: "/products/seer",
    price: <span>₹590</span>,
  };

  it("shows the server's offer, freshness and meta lines and an ADD button", async () => {
    const onAdd = vi.fn();
    const { container } = render(
      <FoodCard
        {...base}
        offer="₹90 OFF"
        freshness="Next slot today, 4 pm"
        meta="500 g · serves 3"
        onAdd={onAdd}
      />,
    );
    expect(screen.getByRole("link", { name: "Seer fish steaks" })).toHaveAttribute(
      "href",
      "/products/seer",
    );
    expect(screen.getByText("₹90 OFF")).toBeInTheDocument();
    expect(screen.getByText("Next slot today, 4 pm")).toBeInTheDocument();
    expect(screen.getByText("500 g · serves 3")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Add Seer fish steaks to cart" }));
    expect(onAdd).toHaveBeenCalledOnce();
    await expectNoAxeViolations(container);
  });

  it("has no offer line when none is given", () => {
    const { container } = render(<FoodCard {...base} addHref="/products/seer" />);
    expect(container.querySelector(".bg-gradient-to-b")).toBeNull();
    expect(
      screen.getByRole("link", { name: "Choose options for Seer fish steaks" }),
    ).toBeInTheDocument();
  });

  it("turns ADD into a stepper once the item is in the cart", async () => {
    const onQuantityChange = vi.fn();
    render(<FoodCard {...base} quantity={2} min={1} max={5} onQuantityChange={onQuantityChange} />);
    expect(screen.queryByRole("button", { name: /^Add / })).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: /Increase/ }));
    expect(onQuantityChange).toHaveBeenCalledWith(3);
  });

  it("shows the unavailable label instead of ADD", () => {
    render(<FoodCard {...base} onAdd={() => {}} unavailableLabel="Out of stock" />);
    expect(screen.getByText("Out of stock")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Add/ })).toBeNull();
  });
});

describe("AppBand", () => {
  it("lists configured store links", () => {
    render(
      <AppBand
        title="Get the app"
        stores={[{ label: "Google Play", href: "https://example.test/play" }]}
        slot="ONBOARDING_DELIVERY"
      />,
    );
    expect(screen.getByRole("link", { name: "Google Play" })).toHaveAttribute(
      "href",
      "https://example.test/play",
    );
  });

  it("shows the fallback instead of inventing links", () => {
    render(<AppBand title="Get the app" fallback="Coming soon" />);
    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByText("Coming soon")).toBeInTheDocument();
  });
});

describe("PageBand and CharacterState", () => {
  it("render their headings at the given levels", async () => {
    const { container } = render(
      <>
        <PageBand eyebrow="Order FO-1" title="Your cart" />
        <CharacterState
          slot="EMPTY_CART"
          title="Basket's empty"
          description="Nothing in here yet."
          actions={<a href="/shop">Shop</a>}
        />
      </>,
    );
    expect(screen.getByRole("heading", { level: 1, name: "Your cart" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Basket's empty" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Shop" })).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });
});
