import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { getGrievanceOfficer } from "@/server/config";
import { GrievanceOfficerCard } from "./grievance-officer";

function configure(values: Record<string, string>) {
  for (const [name, value] of Object.entries(values)) vi.stubEnv(name, value);
}

const complete = {
  FRESH_GRIEVANCE_OFFICER_NAME: "A. Officer",
  FRESH_GRIEVANCE_OFFICER_EMAIL: "grievance@example.in",
  FRESH_GRIEVANCE_RESPONSE_TIME: "Within 48 hours",
};

describe("grievance officer (L-7)", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("shows the configured name, email and response time", () => {
    configure({ ...complete, FRESH_GRIEVANCE_OFFICER_DESIGNATION: "Grievance Officer" });

    const officer = getGrievanceOfficer();
    expect(officer).not.toBeNull();
    render(<GrievanceOfficerCard officer={officer!} />);

    expect(screen.getByText("A. Officer")).toBeInTheDocument();
    expect(screen.getByText("Within 48 hours")).toBeInTheDocument();
    expect(document.querySelector('a[href="mailto:grievance@example.in"]')).not.toBeNull();
    expect(document.getElementById("grievance")).not.toBeNull();
  });

  it("is not published until name, email and response time are all set", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    expect(getGrievanceOfficer()).toBeNull();

    configure({ ...complete, FRESH_GRIEVANCE_RESPONSE_TIME: " " });
    expect(getGrievanceOfficer()).toBeNull();
    warn.mockRestore();
  });
});
