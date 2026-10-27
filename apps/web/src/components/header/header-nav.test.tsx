import { screen, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
import { HeaderNav } from "./header-nav";

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

describe("HeaderNav", () => {
  it("hides the Offers link when the server has no banners", async () => {
    let asked = false;
    server.use(
      http.get(`${API}/promotions`, () => {
        asked = true;
        return HttpResponse.json(page([]));
      }),
    );
    renderWithProviders(<HeaderNav />);

    await waitFor(() => expect(asked).toBe(true));
    expect(screen.getByRole("link", { name: "Help" })).toHaveAttribute("href", "/support");
    expect(screen.queryByRole("link", { name: "Offers" })).not.toBeInTheDocument();
  });

  it("links Offers to the home's offers when the server has a banner", async () => {
    server.use(
      http.get(`${API}/promotions`, () =>
        HttpResponse.json(
          page([
            {
              id: "b1",
              title: "Weekend seer fish",
              subtitle: null,
              linkType: "NONE",
              linkTarget: null,
              linkSlug: null,
            },
          ]),
        ),
      ),
    );
    renderWithProviders(<HeaderNav />);

    expect(await screen.findByRole("link", { name: "Offers" })).toHaveAttribute("href", "/#offers");
  });
});
