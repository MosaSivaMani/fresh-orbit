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
    expect(screen.getByText("today's catch")).toBeInTheDocument();
    expect(screen.getByText("to your kitchen")).toBeInTheDocument();
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

  it("shows the owner's harbour picture instead of the drawn scenery and the Padma picture", () => {
    const { container } = render(
      <HomeHero
        harbour={{
          src: "/illustrations/harbour.jpg",
          srcSet: "/home/harbour-1280.jpg 1280w, /illustrations/harbour.jpg 2120w",
          alt: "Padma at the harbour",
        }}
      />,
    );

    const images = screen.getAllByRole("img");
    expect(images).toHaveLength(1);
    expect(images[0]).toHaveAccessibleName("Padma at the harbour");
    expect(images[0]).toHaveAttribute("srcset", expect.stringContaining("1280w"));
    expect(container.querySelector("img[src='/home/hero-catch.png']")).toBeNull();
    // The heading and chips stay HTML text over the picture.
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/Fresh fish\./);
    expect(screen.getByText("to your kitchen")).toBeInTheDocument();
  });
});
