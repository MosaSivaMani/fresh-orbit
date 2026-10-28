import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "../../../test/render";
import { navigationState, routerMock } from "../../../test/router-mock";
import { type PreparationMethod } from "@/api/types/catalog";
import { parseShopFilters } from "@/lib/shop-params";
import { ShopToolbar } from "./shop-filters";

const methods: PreparationMethod[] = [
  { code: "CURRY_CUT", name: "Curry cut", description: "Bone-in pieces", sortOrder: 40 },
  { code: "STEAK", name: "Steaks", description: null, sortOrder: 50 },
];

describe("ShopToolbar preparation filter (W8)", () => {
  it("lists the server's methods and puts the chosen code in the URL", async () => {
    navigationState.pathname = "/shop";
    renderWithProviders(
      <ShopToolbar
        chips={[]}
        filters={parseShopFilters({})}
        hasLocation
        hasQuery={false}
        preparations={methods}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "More filters" }));
    const sheet = await screen.findByRole("dialog");
    await userEvent.click(within(sheet).getByRole("checkbox", { name: "Curry cut" }));

    await waitFor(() =>
      expect(routerMock.push.mock.calls.at(-1)?.[0]).toBe("/shop?prep=CURRY_CUT"),
    );
  });

  it("shows a chosen method as checked", async () => {
    renderWithProviders(
      <ShopToolbar
        chips={[]}
        filters={parseShopFilters({ prep: "STEAK" })}
        hasLocation
        hasQuery={false}
        preparations={methods}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "More filters" }));
    const sheet = await screen.findByRole("dialog");
    expect(within(sheet).getByRole("checkbox", { name: "Steaks" })).toBeChecked();
    expect(within(sheet).getByRole("checkbox", { name: "Curry cut" })).not.toBeChecked();
  });

  it("leaves the filter out when the server lists no methods", async () => {
    renderWithProviders(
      <ShopToolbar chips={[]} filters={parseShopFilters({})} hasLocation hasQuery={false} />,
    );

    await userEvent.click(screen.getByRole("button", { name: "More filters" }));
    const sheet = await screen.findByRole("dialog");
    expect(within(sheet).queryByRole("checkbox", { name: "Curry cut" })).toBeNull();
  });
});
