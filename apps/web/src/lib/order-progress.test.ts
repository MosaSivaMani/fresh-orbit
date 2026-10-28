import { describe, expect, it } from "vitest";
import { orderSlot, orderSteps } from "./order-progress";

describe("orderSteps", () => {
  it("lights the usual path up to the server's status, with recorded times", () => {
    const steps = orderSteps("PACKED", [
      { status: "CONFIRMED", at: "2026-10-23T05:41:12Z" },
      { status: "PREPARING", at: "2026-10-23T06:10:00Z" },
      { status: "PACKED", at: "2026-10-23T07:00:00Z" },
    ]);
    expect(steps.map((s) => [s.key, s.state])).toEqual([
      ["CONFIRMED", "done"],
      ["PREPARING", "done"],
      ["PACKED", "current"],
      ["OUT_FOR_DELIVERY", "upcoming"],
      ["DELIVERED", "upcoming"],
    ]);
    expect(steps[0]!.time).toBeTruthy();
    expect(steps[3]!.time).toBeUndefined();
  });

  it("shows recorded times in the store's time zone from the server (W10)", () => {
    const timeline = [{ status: "CONFIRMED" as const, at: "2026-10-23T19:00:00Z" }];
    // 19:00 UTC is 24 Oct in IST and 23 Oct in UTC.
    expect(orderSteps("CONFIRMED", timeline)[0]!.time).toContain("24 Oct");
    expect(orderSteps("CONFIRMED", timeline, "UTC")[0]!.time).toContain("23 Oct");
  });

  it("marks every step done once delivered", () => {
    expect(orderSteps("DELIVERED", []).every((s) => s.state === "done")).toBe(true);
  });

  it("shows the timeline as recorded for an order off the usual path", () => {
    const steps = orderSteps("CANCELLED", [
      { status: "CONFIRMED", at: "2026-10-23T05:41:12Z" },
      { status: "CANCELLED", at: "2026-10-23T05:50:00Z" },
    ]);
    expect(steps.map((s) => [s.label, s.state])).toEqual([
      ["Confirmed", "done"],
      ["Cancelled", "current"],
    ]);
  });
});

describe("orderSlot", () => {
  it("picks the character for the status", () => {
    expect(orderSlot("CONFIRMED")).toBe("ORDER_PACKING");
    expect(orderSlot("OUT_FOR_DELIVERY")).toBe("ORDER_ON_THE_WAY");
    expect(orderSlot("DELIVERED")).toBe("ORDER_DELIVERED");
    expect(orderSlot("CANCELLED")).toBe("ORDER_CANCELLED");
  });
});
