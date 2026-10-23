import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { expectNoAxeViolations } from "../../test/axe";
import { Button, IconButton } from "./button";
import { Checkbox, RadioGroup, Switch } from "./choice";
import { Combobox } from "./combobox";
import { FormField } from "./form-field";
import { Input, Textarea } from "./input";
import { Select } from "./select";

describe("Button", () => {
  it("is a type=button by default and fires onClick", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);
    const button = screen.getByRole("button", { name: "Save" });
    expect(button).toHaveAttribute("type", "button");
    await userEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("disables and marks busy while loading", async () => {
    const onClick = vi.fn();
    render(
      <Button loading onClick={onClick}>
        Place order
      </Button>,
    );
    const button = screen.getByRole("button", { name: "Place order" });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
    await userEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("renders a link with button styles via asChild", () => {
    render(
      <Button asChild>
        <a href="/shop">Shop now</a>
      </Button>,
    );
    expect(screen.getByRole("link", { name: "Shop now" })).toHaveAttribute("href", "/shop");
  });

  it("IconButton exposes its label as the accessible name", () => {
    render(<IconButton label="Delete address" icon={<svg aria-hidden="true" />} />);
    expect(screen.getByRole("button", { name: "Delete address" })).toBeInTheDocument();
  });
});

describe("FormField", () => {
  it("wires label, hint, error and required to the input", async () => {
    render(
      <FormField label="Pincode" hint="6 digits" error="Pincode is not serviceable." required>
        <Input />
      </FormField>,
    );
    const input = screen.getByRole("textbox", { name: /Pincode/ });
    expect(input).toBeRequired();
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("6 digits Pincode is not serviceable.");
    await expectNoAxeViolations();
  });

  it("works for textarea and has no axe violations without error", async () => {
    render(
      <FormField label="Delivery instructions">
        <Textarea />
      </FormField>,
    );
    const textarea = screen.getByRole("textbox", { name: "Delivery instructions" });
    expect(textarea).not.toHaveAttribute("aria-invalid");
    await expectNoAxeViolations();
  });
});

describe("choices", () => {
  it("Checkbox toggles via its label", async () => {
    const onChange = vi.fn();
    render(<Checkbox label="Save this address" onCheckedChange={onChange} />);
    await userEvent.click(screen.getByText("Save this address"));
    expect(onChange).toHaveBeenCalledWith(true);
    expect(screen.getByRole("checkbox", { name: "Save this address" })).toBeChecked();
  });

  it("RadioGroup is labelled and selectable by keyboard", async () => {
    const onChange = vi.fn();
    render(
      <RadioGroup
        label="Payment method"
        defaultValue="COD"
        onValueChange={onChange}
        options={[
          { value: "COD", label: "Cash on delivery" },
          { value: "UPI_ON_DELIVERY", label: "UPI on delivery" },
        ]}
      />,
    );
    expect(screen.getByRole("radiogroup", { name: "Payment method" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: "UPI on delivery" }));
    expect(onChange).toHaveBeenCalledWith("UPI_ON_DELIVERY");
    await expectNoAxeViolations();
  });

  it("Switch reports state", async () => {
    render(<Switch label="Accept orders" />);
    const toggle = screen.getByRole("switch", { name: "Accept orders" });
    expect(toggle).not.toBeChecked();
    await userEvent.click(toggle);
    expect(toggle).toBeChecked();
    await expectNoAxeViolations();
  });
});

describe("Select and Combobox", () => {
  it("Select takes its name from the FormField", () => {
    render(
      <FormField label="City">
        <Select options={[{ value: "c1", label: "Chennai" }]} />
      </FormField>,
    );
    expect(screen.getByRole("combobox", { name: "City" })).toBeInTheDocument();
  });

  it("Combobox filters by typing and selects with Enter", async () => {
    const onChange = vi.fn();
    render(
      <Combobox
        aria-label="Product"
        value={null}
        onValueChange={onChange}
        options={[
          { value: "seer", label: "Seer fish", keywords: ["vanjaram"] },
          { value: "pomfret", label: "Pomfret" },
        ]}
      />,
    );
    const trigger = screen.getByRole("combobox", { name: "Product" });
    await userEvent.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    await userEvent.keyboard("vanj");
    expect(screen.queryByText("Pomfret")).not.toBeInTheDocument();
    await userEvent.keyboard("{Enter}");
    expect(onChange).toHaveBeenCalledWith("seer");
  });
});
