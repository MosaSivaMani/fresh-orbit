import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CardPicture, pictureSrcSet } from "./card-picture";

const url = (width: number) => `https://api.test/media/p1/${width}.webp`;
const sources = [1280, 320, 640].map((width) => ({ width, url: url(width) }));

describe("CardPicture (W-9, D375)", () => {
  it("offers the server's renditions as srcset, smallest first, with the drawn size", () => {
    const picture = <CardPicture src={url(640)} sources={sources} alt="" sizes="96px" />;
    const { container } = render(picture);

    const img = container.querySelector("img");
    const expected = [320, 640, 1280].map((w) => `${url(w)} ${w}w`).join(", ");
    expect(img?.getAttribute("srcset")).toBe(expected);
    expect(img?.getAttribute("sizes")).toBe("96px");
    expect(img?.getAttribute("src")).toBe(url(640));
  });

  it("builds no srcset when the server sent no renditions", () => {
    expect(pictureSrcSet(undefined)).toBeUndefined();
    expect(pictureSrcSet([])).toBeUndefined();
  });
});
