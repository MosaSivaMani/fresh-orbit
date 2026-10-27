import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HomeHero } from "./home-hero";

describe("HomeHero", () => {
  it("shows the headline as the page's only h1 and Padma with alt text", () => {
    render(<HomeHero />);

    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent(/Fresh fish\./);
    expect(heading).toHaveTextContent(/Cleaned your way\./);
    expect(heading).toHaveTextContent(/At your door, in your slot\./);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("img", { name: /Padma/ })).toBeInTheDocument();
    expect(screen.getByText(/From today's catch/)).toBeInTheDocument();
  });

  it("lists the three feature chips and asks for a slot at checkout without a server slot", () => {
    render(<HomeHero />);

    const chips = within(screen.getByRole("list", { name: "Why Fresh Orbitt" })).getAllByRole(
      "listitem",
    );
    expect(chips).toHaveLength(3);
    expect(chips[0]).toHaveTextContent(/CLEANED.*Your way.*Whole · Steaks · Curry Cut/);
    expect(chips[1]).toHaveTextContent(/PAY FOR.*Net weight.*Weighed after cleaning/);
    expect(chips[2]).toHaveTextContent(/DELIVERY.*In your slot.*Pick a slot at checkout/);
  });

  it("shows the server's next slot in the delivery chip when there is one", () => {
    render(<HomeHero nextSlot="Sat 25 Oct, 7:00 am – 9:00 am" />);

    expect(screen.getByText("Next slot Sat 25 Oct, 7:00 am – 9:00 am")).toBeInTheDocument();
    expect(screen.queryByText("Pick a slot at checkout")).toBeNull();
  });

  it("hides every drawing from assistive technology", () => {
    const { container } = render(<HomeHero />);

    const svgs = Array.from(container.querySelectorAll("svg"));
    expect(svgs.length).toBeGreaterThan(0);
    for (const svg of svgs) {
      expect(svg.closest("[aria-hidden='true']")).not.toBeNull();
    }
  });

  it("puts the optional background photo behind the hero as a decorative image", () => {
    const { container } = render(<HomeHero heroBackground="/home/harbour.jpg" />);

    const images = Array.from(container.querySelectorAll("img"));
    expect(images.some((img) => img.getAttribute("alt") === "")).toBe(true);
    // Only Padma is announced.
    expect(screen.getAllByRole("img")).toHaveLength(1);
  });
});
