import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { cartWithSeer, notServiceable, problem, serviceable } from "../../test/fixtures";
import { API } from "../../test/msw/handlers";
import { server } from "../../test/msw/server";
import { renderWithProviders } from "../../test/render";
import { routerMock } from "../../test/router-mock";
import { PINCODE_COOKIE, readBrowserCookie } from "@/lib/cookies";
import { PincodeForm } from "./pincode-form";

describe("PincodeForm", () => {
  it("stores a serviceable pincode, refreshes server pages and shows the server's delivery terms", async () => {
    server.use(
      http.get(`${API}/serviceability`, ({ request }) => {
        expect(new URL(request.url).searchParams.get("pincode")).toBe("600041");
        return HttpResponse.json(serviceable);
      }),
    );
    const onDone = vi.fn();
    renderWithProviders(<PincodeForm onDone={onDone} />);

    await userEvent.type(screen.getByLabelText("Delivery pincode"), "600041");
    await userEvent.click(screen.getByRole("button", { name: "Check" }));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(readBrowserCookie(PINCODE_COOKIE)).toBe("600041");
    expect(routerMock.refresh).toHaveBeenCalled();
    expect(await screen.findByText(/We deliver to 600041, Chennai/)).toBeInTheDocument();
    // Fee and minimum come from the server response, formatted only.
    expect(screen.getByText(/Delivery fee ₹39/)).toBeInTheDocument();
  });

  it("does not store an unserviceable pincode and says so", async () => {
    server.use(http.get(`${API}/serviceability`, () => HttpResponse.json(notServiceable)));
    renderWithProviders(<PincodeForm />);

    await userEvent.type(screen.getByLabelText("Delivery pincode"), "600123");
    await userEvent.click(screen.getByRole("button", { name: "Check" }));

    expect(await screen.findByText("We do not deliver to 600123 yet.")).toBeInTheDocument();
    expect(readBrowserCookie(PINCODE_COOKIE)).toBeNull();
    expect(routerMock.refresh).not.toHaveBeenCalled();
  });

  it("refuses a malformed pincode before calling the server", async () => {
    renderWithProviders(<PincodeForm />);
    await userEvent.type(screen.getByLabelText("Delivery pincode"), "1234");
    await userEvent.click(screen.getByRole("button", { name: "Check" }));
    expect(screen.getByLabelText("Delivery pincode")).toHaveAttribute("aria-invalid", "true");
  });

  it("moves the cart and asks before dropping lines not sold at the new location (409 CART_LOCATION_CHANGED)", async () => {
    const other = { ...serviceable, pincode: "600020" };
    const locationCalls: string[] = [];
    server.use(
      http.get(`${API}/cart`, () => HttpResponse.json(cartWithSeer)),
      http.get(`${API}/serviceability`, () => HttpResponse.json(other)),
      http.put(`${API}/cart/location`, async ({ request }) => {
        const force = new URL(request.url).searchParams.get("force");
        locationCalls.push(force ?? "none");
        expect(await request.json()).toEqual({ pincode: "600020" });
        if (force !== "true") {
          return HttpResponse.json(
            problem(409, "CART_LOCATION_CHANGED", {
              lines: [{ id: "line1", code: "PRICE_NOT_AVAILABLE" }],
            }),
            { status: 409, headers: { "Content-Type": "application/problem+json" } },
          );
        }
        return HttpResponse.json({ ...cartWithSeer, pincode: "600020", lines: [] });
      }),
    );
    const onDone = vi.fn();
    const { queryClient } = renderWithProviders(<PincodeForm onDone={onDone} />);
    // Wait for the cart to load so the form knows it has lines.
    await waitFor(() => expect(queryClient.getQueryData(["cart"])).toBeDefined());

    await userEvent.type(screen.getByLabelText("Delivery pincode"), "600020");
    await userEvent.click(screen.getByRole("button", { name: "Check" }));

    expect(await screen.findByText("Seer Fish (Vanjaram)")).toBeInTheDocument();
    expect(onDone).not.toHaveBeenCalled();
    expect(readBrowserCookie(PINCODE_COOKIE)).toBeNull();

    await userEvent.click(screen.getByRole("button", { name: "Change pincode and remove them" }));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(locationCalls).toEqual(["none", "true"]);
    expect(readBrowserCookie(PINCODE_COOKIE)).toBe("600020");
  });
});
