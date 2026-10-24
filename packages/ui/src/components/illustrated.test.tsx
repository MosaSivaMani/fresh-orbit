import { type IllustrationEntry, illustrations } from "@fresh/illustrations";
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { Button } from "./button";
import { EmptyState } from "./display";
import {
  FeatureCard,
  HelpHero,
  HeroBanner,
  Illustration,
  IllustrationProvider,
  PolaroidCard,
  ReceiptCard,
  Sticker,
  isSceneIllustration,
  resolveIllustration,
} from "./illustrated";

/** The EMPTY_CART entry as the manifest has it now (placeholder or imported final). */
const placeholder = illustrations.EMPTY_CART;

describe("Illustration", () => {
  it("uses the manifest's file, alt text and size, and loads lazily", async () => {
    render(<Illustration slot="EMPTY_CART" size="md" />);
    const img = screen.getByRole("img", { name: placeholder.alt });
    expect(img).toHaveAttribute("src", `/illustrations/${placeholder.src}`);
    expect(img).toHaveAttribute("width", "240");
    expect(img).toHaveAttribute("loading", "lazy");
    expect(img).toHaveAttribute("data-status", placeholder.status);
    await expectNoAxeViolations();
  });

  it("loads eagerly with high priority for above-the-fold pictures and keeps the 4:3 ratio", () => {
    render(<Illustration slot="HOME_HERO" size="xl" priority />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("loading", "eager");
    expect(img).toHaveAttribute("fetchpriority", "high");
    expect(img).toHaveAttribute("width", "480");
    expect(img).toHaveAttribute("height", "360");
  });

  it("is hidden from screen readers in decorative mode", () => {
    const { container } = render(<Illustration slot="EMPTY_CART" decorative />);
    expect(screen.queryByRole("img")).toBeNull();
    expect(container.querySelector("img")).toHaveAttribute("alt", "");
  });

  it("loads from the provider's base URL", () => {
    render(
      <IllustrationProvider baseUrl="https://cdn.example.test/ill/">
        <Illustration slot="OFFLINE" />
      </IllustrationProvider>,
    );
    expect(screen.getByRole("img")).toHaveAttribute(
      "src",
      `https://cdn.example.test/ill/${illustrations.OFFLINE.src}`,
    );
  });

  it("uses the final file and its real size once the slot is FINAL", () => {
    const finalEntry: IllustrationEntry = {
      ...placeholder,
      src: "empty_cart.jpg",
      status: "FINAL",
      background: "SCENE",
      width: 1448,
      height: 1086,
    };
    const manifest = { EMPTY_CART: finalEntry };
    expect(resolveIllustration("EMPTY_CART", "/illustrations", "fill", manifest)).toMatchObject({
      src: "/illustrations/empty_cart.jpg",
      width: 1448,
      height: 1086,
    });
    expect(resolveIllustration("EMPTY_CART", "/illustrations", "sm", manifest)).toMatchObject({
      width: 160,
      height: 120,
    });
    expect(isSceneIllustration("EMPTY_CART", manifest)).toBe(true);
    expect(
      isSceneIllustration("EMPTY_CART", {
        EMPTY_CART: { ...placeholder, status: "PLACEHOLDER", background: "TRANSPARENT" },
      }),
    ).toBe(false);
  });

  it("crops a scene with object-fit cover in a rounded frame and shows anything else whole", () => {
    const { rerender } = render(<Illustration slot="EMPTY_CART" fit="cover" size="sm" />);
    expect(screen.getByRole("img").className).toContain("object-cover");
    expect(screen.getByRole("img").className).not.toContain("rounded");
    rerender(<Illustration slot="EMPTY_CART" fit="contain" size="sm" />);
    expect(screen.getByRole("img").className).toContain("object-contain");
    rerender(<Illustration slot="EMPTY_CART" size="sm" float />);
    const img = screen.getByRole("img");
    expect(img.className).toContain(
      placeholder.background === "SCENE" ? "rounded-2xl" : "motion-safe:animate-fo-float",
    );
  });
});

describe("EmptyState with a slot", () => {
  it("shows the illustration as decoration and the title as a heading", async () => {
    const { container } = render(
      <EmptyState
        slot="EMPTY_CART"
        headingLevel={2}
        title="Your cart is empty"
        description="Browse today's catch."
        action={<Button>Browse fish</Button>}
      />,
    );
    expect(screen.getByRole("heading", { level: 2, name: "Your cart is empty" })).toBeVisible();
    expect(container.querySelector('img[data-slot="EMPTY_CART"]')).toHaveAttribute("alt", "");
    await expectNoAxeViolations();
  });
});

describe("FeatureCard", () => {
  it("shows a picture, a heading and a line, with no step number", async () => {
    render(
      <FeatureCard
        slot="FEATURE_COLD_CHAIN"
        tone="ice"
        title="Cold all the way"
        description="Packed on ice."
      />,
    );
    const card = screen.getByRole("article");
    expect(within(card).getByRole("heading", { level: 3, name: "Cold all the way" })).toBeVisible();
    expect(card.textContent).not.toMatch(/\d/);
    expect(card.querySelector("div")?.className).toContain("bg-ice");
    await expectNoAxeViolations();
  });
});

describe("PolaroidCard and Sticker", () => {
  it("captions the picture and renders sticker labels as text", async () => {
    render(
      <PolaroidCard
        slot="FEATURE_HONEST_WEIGHT"
        caption="Weighed in front of you"
        stickers={[
          { label: "Honest weight", corner: "top-left" },
          { label: "Fresh today", tone: "teal", corner: "bottom-right" },
        ]}
      />,
    );
    const figure = screen.getByRole("figure", { name: "Weighed in front of you" });
    expect(within(figure).getByText("Honest weight")).toBeVisible();
    expect(within(figure).getByText("Fresh today")).toBeVisible();
    expect(within(figure).getByRole("img")).toHaveAccessibleName(
      illustrations.FEATURE_HONEST_WEIGHT.alt,
    );
    await expectNoAxeViolations();
  });

  it("tilts only on wide screens with motion allowed", () => {
    const { rerender } = render(<PolaroidCard slot="HOME_HERO" caption="Our stall" />);
    expect(screen.getByRole("figure").className).toContain("md:motion-safe:-rotate-[2.5deg]");
    rerender(<PolaroidCard slot="HOME_HERO" caption="Our stall" tilt={false} />);
    expect(screen.getByRole("figure").className).not.toContain("rotate");
  });

  it("renders a sticker tone with the ink outline", () => {
    render(<Sticker tone="mango">Bestseller</Sticker>);
    const sticker = screen.getByText("Bestseller");
    expect(sticker.className).toContain("bg-mango");
    expect(sticker.className).toContain("border-outline-ink");
    expect(sticker.className).toContain("font-extrabold");
  });
});

describe("HelpHero", () => {
  it("is a labelled section with a heading and actions", async () => {
    render(
      <HelpHero
        title="How can we help you today?"
        description="We answer within a few minutes."
        actions={<Button>Chat with us</Button>}
      />,
    );
    expect(screen.getByRole("region", { name: "How can we help you today?" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "How can we help you today?",
    );
    await expectNoAxeViolations();
  });
});

describe("HeroBanner", () => {
  it("is a labelled section whose picture keeps its alt text", async () => {
    render(
      <HeroBanner
        slot="ONBOARDING_FRESH"
        title="Fresh from the harbour"
        description="Caught at dawn, at your door by lunch."
        actions={<Button>Shop today's catch</Button>}
        stickers={[{ label: "Caught today", corner: "top-left" }]}
      />,
    );
    const hero = screen.getByRole("region", { name: "Fresh from the harbour" });
    const img = within(hero).getByRole("img", { name: illustrations.ONBOARDING_FRESH.alt });
    expect(img).toHaveAttribute("loading", "eager");
    expect(img.className).toContain(
      isSceneIllustration("ONBOARDING_FRESH") ? "object-cover" : "object-contain",
    );
    expect(within(hero).getByText("Caught today")).toBeVisible();
    await expectNoAxeViolations();
  });
});

describe("ReceiptCard", () => {
  it("renders exactly the values passed in and computes nothing", async () => {
    render(
      <ReceiptCard
        title="Order summary"
        meta="Order FO-1042"
        sections={[
          [
            { id: "a", label: "Seer fish", description: "500 g, curry cut", value: "₹649.00" },
            { id: "b", label: "Prawns", value: "₹380.00" },
          ],
          [
            { id: "c", label: "Coupon", value: "−₹50.00", tone: "saving" },
            { id: "d", label: "Delivery", value: "Free", tone: "muted" },
          ],
        ]}
        // Deliberately not the sum of the lines: the card must show the server's total as given.
        total={{ label: "Total", value: "₹1,000.00" }}
        note="Weighed 12 g over, well under the 10% cap."
      />,
    );
    const receipt = screen.getByRole("region", { name: "Order summary" });
    expect(within(receipt).getByText("₹649.00")).toBeVisible();
    expect(within(receipt).getByText("−₹50.00")).toBeVisible();
    expect(within(receipt).getByText("₹1,000.00")).toBeVisible();
    expect(within(receipt).queryByText("₹979.00")).toBeNull();
    expect(within(receipt).getByText("₹380.00").className).toContain("tabular-nums");
    await expectNoAxeViolations();
  });
});
