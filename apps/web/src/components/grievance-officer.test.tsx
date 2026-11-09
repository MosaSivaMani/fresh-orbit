import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { type PublicBusinessProfile } from "@/api/types/locations";
import { GrievanceOfficerCard, grievanceOfficerOf } from "./grievance-officer";

const profile: PublicBusinessProfile = {
  legalName: "Example Seafoods Private Limited",
  tradeName: "Example",
  gstin: null,
  fssaiLicenseNo: null,
  address: "1 Harbour Road",
  supportPhone: "+919876543210",
  supportEmail: "help@example.in",
  supportHours: null,
  timeZone: "Asia/Kolkata",
};

const officer = {
  name: "A. Officer",
  designation: "Grievance Officer",
  email: "grievance@example.in",
  phone: "+919876543211",
  responseTime: "Within 48 hours",
};

describe("grievance officer (L-7, D374)", () => {
  it("shows the business profile's name, email, phone and response time", () => {
    const shown = grievanceOfficerOf({ ...profile, grievanceOfficer: officer });
    expect(shown).not.toBeNull();
    render(<GrievanceOfficerCard officer={shown!} />);

    expect(screen.getByText("A. Officer")).toBeInTheDocument();
    expect(screen.getByText("Within 48 hours")).toBeInTheDocument();
    expect(document.querySelector('a[href="mailto:grievance@example.in"]')).not.toBeNull();
    expect(document.querySelector('a[href="tel:+919876543211"]')).not.toBeNull();
    expect(document.getElementById("grievance")).not.toBeNull();
  });

  it("is not published until the business profile names an officer with an email", () => {
    expect(grievanceOfficerOf(null)).toBeNull();
    expect(grievanceOfficerOf(profile)).toBeNull();
    expect(grievanceOfficerOf({ ...profile, grievanceOfficer: null })).toBeNull();
    const noEmail = { ...officer, email: " " };
    expect(grievanceOfficerOf({ ...profile, grievanceOfficer: noEmail })).toBeNull();
  });

  it("leaves out the phone and response time when they are not entered", () => {
    const bare = { name: "A. Officer", email: "grievance@example.in" };
    render(<GrievanceOfficerCard officer={bare} />);

    expect(document.querySelector('#grievance a[href^="tel:"]')).toBeNull();
    expect(screen.queryByText("Within 48 hours")).toBeNull();
  });
});
