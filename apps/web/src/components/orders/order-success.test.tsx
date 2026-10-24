import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { API } from "../../../test/msw/handlers";
import { server } from "../../../test/msw/server";
import { renderWithProviders, signedIn } from "../../../test/render";
import { OrderSuccess } from "./order-success";

const estimate = {
  itemsPaise: 64950,
  preparationPaise: 2000,
  discountPaise: 0,
  deliveryFeePaise: 3900,
  taxPaise: 0,
  totalPaise: 70850,
};

describe("OrderSuccess", () => {
  it("shows the order-placed character, the order number and the server's estimate", async () => {
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json({
          id: "o1",
          orderNumber: "FO-260001",
          status: "CONFIRMED",
          paymentMethod: "UPI_ON_DELIVERY",
          slot: { startAt: "2026-10-23T08:30:00Z", endAt: "2026-10-23T10:30:00Z" },
          estimate,
          final: null,
        }),
      ),
    );
    const { container } = renderWithProviders(<OrderSuccess orderId="o1" />, {
      session: signedIn,
    });

    expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Order placed!");
    expect(container.querySelector("img")?.getAttribute("src")).toContain("order_placed");
    expect(screen.getByText("FO-260001")).toBeInTheDocument();
    expect(screen.getByText("UPI on delivery")).toBeInTheDocument();
    const bill = screen.getByRole("region", { name: "Bill" });
    expect(within(bill).getByText("₹39")).toBeInTheDocument();
    expect(within(bill).getByText("₹708.50")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Track order" })).toHaveAttribute("href", "/orders/o1");
  });
});
