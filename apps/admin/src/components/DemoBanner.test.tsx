import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DemoBanner, isDemoMode } from "./DemoBanner";

describe("DemoBanner", () => {
  it("tells staff the dashboard is test data when the demo mode is on", () => {
    render(<DemoBanner enabled />);

    const banner = screen.getByRole("note", { name: "Demo dashboard" });
    expect(banner).toHaveTextContent(/DEMO, test data only/);
  });

  it("renders nothing outside the demo", () => {
    const { container } = render(<DemoBanner enabled={false} />);

    expect(container).toBeEmptyDOMElement();
  });

  it("is on only for the exact value true", () => {
    expect(isDemoMode("true")).toBe(true);
    expect(isDemoMode("false")).toBe(false);
    expect(isDemoMode("")).toBe(false);
    expect(isDemoMode(undefined)).toBe(false);
  });
});
