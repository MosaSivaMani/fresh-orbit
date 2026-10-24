import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { isDemoMode } from "@/server/config";
import { DemoBanner } from "./demo-banner";

describe("DemoBanner", () => {
  it("tells visitors the store is test data when the demo mode is on", () => {
    render(<DemoBanner enabled />);

    const banner = screen.getByRole("note", { name: "Demo store" });
    expect(banner).toHaveTextContent(/DEMO, test data only/);
  });

  it("renders nothing outside the demo", () => {
    const { container } = render(<DemoBanner enabled={false} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe("isDemoMode", () => {
  const value = process.env.NEXT_PUBLIC_DEMO_MODE;
  afterEach(() => {
    if (value === undefined) delete process.env.NEXT_PUBLIC_DEMO_MODE;
    else process.env.NEXT_PUBLIC_DEMO_MODE = value;
  });

  it("is on only for the exact value true", () => {
    process.env.NEXT_PUBLIC_DEMO_MODE = "true";
    expect(isDemoMode()).toBe(true);
    process.env.NEXT_PUBLIC_DEMO_MODE = "false";
    expect(isDemoMode()).toBe(false);
    delete process.env.NEXT_PUBLIC_DEMO_MODE;
    expect(isDemoMode()).toBe(false);
  });
});
