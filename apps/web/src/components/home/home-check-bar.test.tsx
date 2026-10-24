import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { serviceable } from "../../../test/fixtures";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
import { HomeCheckBar } from "./home-check-bar";

describe("HomeCheckBar", () => {
  it("offers the pincode check and the product search side by side", () => {
    renderWithProviders(<HomeCheckBar />);

    expect(screen.getByLabelText("Delivery pincode")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Check" })).toBeInTheDocument();
    expect(screen.getByRole("search", { name: "Search products" })).toBeInTheDocument();
  });

  it("shows the server's answer for a pincode", async () => {
    server.use(http.get(`${API}/serviceability`, () => HttpResponse.json(serviceable)));
    renderWithProviders(<HomeCheckBar />);

    await userEvent.type(screen.getByLabelText("Delivery pincode"), "600041");
    await userEvent.click(screen.getByRole("button", { name: "Check" }));

    expect(await screen.findByText(/We deliver to 600041/)).toBeInTheDocument();
  });
});
