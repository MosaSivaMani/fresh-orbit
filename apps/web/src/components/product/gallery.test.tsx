import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { type ProductImage } from "@/api/types/catalog";
import { Gallery } from "./gallery";

const photo: ProductImage = {
  url: "https://api.test/media/p1/1280.webp",
  altText: "Seer fish steaks",
  width: 1600,
  height: 1067,
  sources: [
    { width: 320, url: "https://api.test/media/p1/320.webp" },
    { width: 640, url: "https://api.test/media/p1/640.webp" },
    { width: 1280, url: "https://api.test/media/p1/1280.webp" },
  ],
};

describe("Gallery (W-9)", () => {
  it("lets the browser pick from the server's renditions for slides and thumbnails", () => {
    const { container } = render(<Gallery images={[photo, photo]} name="Seer fish" />);

    const images = Array.from(container.querySelectorAll("img"));
    expect(images.length).toBeGreaterThanOrEqual(2);
    for (const img of images) {
      const srcset = img.getAttribute("srcset") ?? "";
      for (const source of photo.sources)
        expect(srcset).toContain(`${source.url} ${source.width}w`);
      expect(img.getAttribute("sizes")).toBeTruthy();
    }
    expect(images.some((img) => img.getAttribute("sizes") === "64px")).toBe(true);
  });
});
