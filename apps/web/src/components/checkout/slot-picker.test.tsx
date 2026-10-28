import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders } from "../../../test/render";
import { SlotPicker } from "./slot-picker";

function slotsIn(timeZone: string) {
  return {
    fulfillmentLocationId: "l1",
    timeZone,
    items: [
      {
        id: "slot1",
        slotDate: "2026-10-23",
        // 02:30 UTC is 8:00 am in India and 2:30 am in UTC.
        startAt: "2026-10-23T02:30:00Z",
        endAt: "2026-10-23T04:30:00Z",
        cutoffAt: "2026-10-22T14:30:00Z",
        status: "AVAILABLE",
      },
    ],
  };
}

describe("SlotPicker (W10)", () => {
  it("shows slot times in the zone the server sends with the slots", async () => {
    server.use(http.get(`${API}/delivery-slots`, () => HttpResponse.json(slotsIn("UTC"))));
    renderWithProviders(<SlotPicker pincode="600041" value={null} onChange={() => {}} />);

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    expect(within(times).getByRole("radio", { name: /^2:30/ })).toBeInTheDocument();
  });

  it("shows India time for an Indian store", async () => {
    server.use(http.get(`${API}/delivery-slots`, () => HttpResponse.json(slotsIn("Asia/Kolkata"))));
    renderWithProviders(<SlotPicker pincode="600041" value={null} onChange={() => {}} />);

    const times = await screen.findByRole("radiogroup", { name: "Delivery time" });
    expect(within(times).getByRole("radio", { name: /^8:00/ })).toBeInTheDocument();
  });
});
