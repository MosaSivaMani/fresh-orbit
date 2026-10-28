import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { ProductGallery } from "./product-gallery";

const images = [
  { url: "/seer-1.jpg", altText: "Seer fish steaks on ice" },
  { url: "/seer-2.jpg", altText: "Seer fish, whole" },
  { url: "/seer-3.jpg", altText: "" },
];

function dots() {
  return within(screen.getByRole("group", { name: "Choose a photo" })).getAllByRole("button");
}

describe("ProductGallery", () => {
  it("shows every photo as a slide in the labelled carousel", async () => {
    render(<ProductGallery images={images} name="Seer fish" />);
    const gallery = screen.getByRole("region", { name: "Seer fish photos" });
    expect(gallery).toHaveAttribute("aria-roledescription", "carousel");
    expect(within(gallery).getByRole("group", { name: "1 of 3" })).toBeInTheDocument();
    expect(within(gallery).getByRole("group", { name: "3 of 3" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Seer fish steaks on ice" })).toHaveAttribute(
      "src",
      "/seer-1.jpg",
    );
    expect(screen.getByRole("img", { name: "Seer fish, whole" })).toBeInTheDocument();
    await expectNoAxeViolations();
  });

  it("falls back to the product name when a photo has no alt text", () => {
    render(<ProductGallery images={images} name="Seer fish" />);
    expect(screen.getByRole("img", { name: "Seer fish" })).toHaveAttribute("src", "/seer-3.jpg");
  });

  it("loads the first photo eagerly and the rest lazily", () => {
    render(<ProductGallery images={images} name="Seer fish" />);
    expect(screen.getByRole("img", { name: "Seer fish steaks on ice" })).toHaveAttribute(
      "loading",
      "eager",
    );
    expect(screen.getByRole("img", { name: "Seer fish, whole" })).toHaveAttribute(
      "loading",
      "lazy",
    );
  });

  it("shows dots and thumbnails only when there is more than one photo", () => {
    const { unmount } = render(<ProductGallery images={images} name="Seer fish" />);
    expect(dots()).toHaveLength(3);
    expect(screen.getByRole("group", { name: "Photo thumbnails" })).toBeInTheDocument();
    unmount();

    render(<ProductGallery images={images.slice(0, 1)} name="Seer fish" />);
    expect(screen.getByRole("group", { name: "1 of 1" })).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Choose a photo" })).toBeNull();
    expect(screen.queryByRole("group", { name: "Photo thumbnails" })).toBeNull();
  });

  it("marks the clicked dot as the current photo", async () => {
    const user = userEvent.setup();
    render(<ProductGallery images={images} name="Seer fish" />);
    expect(dots()[0]).toHaveAttribute("aria-current", "true");

    const third = dots()[2];
    expect(third).toHaveAccessibleName("Show photo 3 of 3");
    await user.click(third!);

    expect(dots()[2]).toHaveAttribute("aria-current", "true");
    expect(dots()[0]).not.toHaveAttribute("aria-current");
  });

  it("moves to the next photo with ArrowRight and wraps around", () => {
    render(<ProductGallery images={images} name="Seer fish" />);
    const gallery = screen.getByRole("region", { name: "Seer fish photos" });

    fireEvent.keyDown(gallery, { key: "ArrowRight" });
    expect(dots()[1]).toHaveAttribute("aria-current", "true");
    fireEvent.keyDown(gallery, { key: "ArrowRight" });
    fireEvent.keyDown(gallery, { key: "ArrowRight" });
    expect(dots()[0]).toHaveAttribute("aria-current", "true");
    fireEvent.keyDown(gallery, { key: "ArrowLeft" });
    expect(dots()[2]).toHaveAttribute("aria-current", "true");
  });

  it("uses the app's image component when one is given", () => {
    render(
      <ProductGallery
        images={images}
        name="Seer fish"
        renderImage={({ alt, variant, priority }) => (
          <span data-testid="picture" data-variant={variant} data-priority={priority}>
            {alt}
          </span>
        )}
      />,
    );
    const pictures = screen.getAllByTestId("picture");
    expect(pictures.filter((p) => p.dataset.variant === "slide")).toHaveLength(3);
    expect(pictures.filter((p) => p.dataset.variant === "thumbnail")).toHaveLength(3);
    expect(pictures.filter((p) => p.dataset.priority === "true")).toHaveLength(1);
  });

  it("says there is no photo yet when the product has none", () => {
    render(<ProductGallery images={[]} name="Seer fish" />);
    expect(screen.getByText("No photo of Seer fish yet")).toBeInTheDocument();
    expect(screen.queryByRole("region")).toBeNull();
    expect(screen.queryByRole("img")).toBeNull();
  });
});
