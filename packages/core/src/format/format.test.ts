import { describe, expect, it } from "vitest";
import {
  formatCalendarDate,
  formatDate,
  formatDateTime,
  formatDayShort,
  formatPercentFromBp,
  formatQuantity,
  formatRupees,
  formatTime,
  formatTimeOfDay,
  formatTimeRange,
  formatWeight,
  formatWeightRange,
  humanizeCode,
} from "./format";

// ICU may use narrow/no-break spaces; compare with plain spaces.
const plain = (s: string) => s.replace(/[\u00a0\u202f]/g, " ");

describe("formatRupees", () => {
  it("formats paise with Indian grouping", () => {
    expect(formatRupees(124950)).toBe("₹1,249.50");
    expect(formatRupees(12499900)).toBe("₹1,24,999");
    expect(formatRupees(0)).toBe("₹0");
  });

  it("controls the paise part", () => {
    expect(formatRupees(50000, { paise: "always" })).toBe("₹500.00");
    expect(formatRupees(50050, { paise: "never" })).toBe("₹501");
  });

  it("handles negatives and bigint without float error", () => {
    expect(formatRupees(-5000)).toBe("-₹50");
    expect(formatRupees(900719925474099301n)).toBe("₹9,00,71,99,25,47,40,993.01");
    expect(formatRupees(10)).toBe("₹0.10");
  });
});

describe("formatWeight / formatQuantity", () => {
  it("chooses g or kg", () => {
    expect(formatWeight(500)).toBe("500 g");
    expect(formatWeight(1000)).toBe("1 kg");
    expect(formatWeight(1250)).toBe("1.25 kg");
    expect(formatWeight(750, { unit: "kg" })).toBe("0.75 kg");
    expect(formatWeight(2000, { unit: "g" })).toBe("2,000 g");
  });

  it("formats ranges", () => {
    expect(formatWeightRange(450, 550)).toBe("450–550 g");
    expect(formatWeightRange(900, 1100)).toBe("0.9–1.1 kg");
    expect(formatWeightRange(500, 500)).toBe("500 g");
  });

  it("formats each quantity unit", () => {
    expect(formatQuantity({ value: 750, unit: "GRAM" })).toBe("750 g");
    expect(formatQuantity({ value: 1, unit: "PIECE" })).toBe("1 piece");
    expect(formatQuantity({ value: 6, unit: "PIECE" })).toBe("6 pieces");
    expect(formatQuantity({ value: 2, unit: "PACK" })).toBe("2 packs");
    expect(formatQuantity({ value: 3, unit: "TRAY_SET" })).toBe("3 tray set");
  });

  it("formats basis points", () => {
    expect(formatPercentFromBp(500)).toBe("5%");
    expect(formatPercentFromBp(1250)).toBe("12.5%");
  });
});

describe("IST dates", () => {
  // 2026-10-23T05:30:00Z is 11:00 in Asia/Kolkata.
  const instant = "2026-10-23T05:30:00Z";

  it("converts UTC instants to IST", () => {
    expect(formatDate(instant)).toBe("23 Oct 2026");
    expect(plain(formatTime(instant)).toLowerCase()).toBe("11:00 am");
    expect(plain(formatDateTime(instant)).toLowerCase()).toBe("23 oct 2026, 11:00 am");
  });

  it("crosses midnight into the next IST day", () => {
    expect(formatDate("2026-10-23T19:00:00Z")).toBe("24 Oct 2026");
  });

  it("keeps calendar dates and times of day as given", () => {
    expect(formatCalendarDate("2026-10-23")).toBe("23 Oct 2026");
    expect(plain(formatTimeOfDay("07:00")).toLowerCase()).toBe("7:00 am");
    expect(plain(formatTimeOfDay("18:30")).toLowerCase()).toBe("6:30 pm");
    expect(plain(formatTimeRange("07:00", "09:00")).toLowerCase()).toBe("7:00 am – 9:00 am");
  });
});

describe("dates in a store's time zone (W10)", () => {
  const instant = "2026-10-23T05:30:00Z";

  it("shows the instant in the zone the server sends", () => {
    expect(plain(formatTime(instant, "Asia/Dubai")).toLowerCase()).toBe("9:30 am");
    expect(plain(formatDateTime(instant, "UTC")).toLowerCase()).toBe("23 oct 2026, 5:30 am");
    // 19:00 UTC is the next day in IST but the same day in UTC.
    expect(formatDate("2026-10-23T19:00:00Z", "UTC")).toBe("23 Oct 2026");
    expect(formatDayShort("2026-10-23T19:00:00Z", "UTC")).toBe(
      formatDayShort("2026-10-23T12:00:00Z"),
    );
  });

  it("falls back to IST when the zone is missing or unknown", () => {
    expect(formatTime(instant, null)).toBe(formatTime(instant));
    expect(formatTime(instant, "")).toBe(formatTime(instant));
    expect(formatTime(instant, "Not/A_Zone")).toBe(formatTime(instant));
  });
});

describe("humanizeCode", () => {
  it("turns UPPER_SNAKE into a sentence", () => {
    expect(humanizeCode("OUT_FOR_DELIVERY")).toBe("Out for delivery");
  });
});
