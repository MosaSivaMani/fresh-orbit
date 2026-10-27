import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
import { AddressForm } from "./address-form";

const cityId = "c0a80101-0000-0000-0000-000000000001";

describe("AddressForm", () => {
  it("fills an empty city and state from the server when the pincode is complete", async () => {
    let asked: string | null = null;
    server.use(
      http.get(`${API}/serviceability`, ({ request }) => {
        asked = new URL(request.url).searchParams.get("pincode");
        return HttpResponse.json({
          pincode: "530017",
          serviceable: true,
          city: {
            id: cityId,
            code: "VSKP",
            name: "Sample City",
            timeZone: "Asia/Kolkata",
            stateCode: "AP",
            stateName: "Sample State",
          },
        });
      }),
    );
    renderWithProviders(<AddressForm onSubmit={vi.fn()} />);

    await userEvent.type(screen.getByLabelText(/Pincode/), "530017");

    await waitFor(() => expect(screen.getByLabelText(/City/)).toHaveValue("Sample City"));
    expect(screen.getByLabelText(/State code/)).toHaveValue("AP");
    expect(asked).toBe("530017");
  });
});
