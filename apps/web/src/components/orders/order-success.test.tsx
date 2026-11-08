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

/** A 500 g weighed line; the server's maximum equals the ordered weight (D347: no over-weight charge). */
const items = [
  {
    id: "i1",
    lineNo: 1,
    variantId: "v1",
    productName: "Seer Fish (Vanjaram)",
    variantName: "Medium (3-5 kg fish)",
    preparationName: "Steaks (1 cm)",
    imageKey: null,
    orderedQuantity: { value: 500, unit: "GRAM" },
    maxChargeableQuantity: { value: 500, unit: "GRAM" },
    packedQuantity: null,
    chargedQuantity: null,
    unitPricePaise: 129900,
    preparationChargePaise: 2000,
    estimatedTotalPaise: 66950,
    finalTotalPaise: null,
    status: "ACTIVE",
    substitutedFromItemId: null,
  },
];

describe("OrderSuccess", () => {
  it("shows the order-placed character, the order number and the server's total", async () => {
    server.use(
      http.get(`${API}/orders/o1`, () =>
        HttpResponse.json({
          id: "o1",
          orderNumber: "FO-260001",
          status: "CONFIRMED",
          paymentMethod: "UPI_ON_DELIVERY",
          slot: { startAt: "2026-10-23T08:30:00Z", endAt: "2026-10-23T10:30:00Z" },
          items,
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
    expect(within(bill).getByText("Total")).toBeInTheDocument();
    expect(within(bill).getByText(/can only come down/)).toBeInTheDocument();
    expect(screen.queryByText(/estimat/i)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Track order" })).toHaveAttribute("href", "/orders/o1");
  });
});
