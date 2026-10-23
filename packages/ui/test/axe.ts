import axe from "axe-core";
import { expect } from "vitest";

/**
 * Run axe-core against a rendered container and fail with a readable list of violations.
 * Colour contrast is checked by the design-token unit test instead (jsdom does not compute styles).
 */
export async function expectNoAxeViolations(container: Element = document.body): Promise<void> {
  const result = await axe.run(container, {
    rules: {
      "color-contrast": { enabled: false },
      // Components are tested in isolation, not as full pages.
      region: { enabled: false },
    },
  });
  const summary = result.violations.map(
    (v) => `${v.id}: ${v.help}\n  ${v.nodes.map((n) => n.target.join(" ")).join("\n  ")}`,
  );
  expect(summary, summary.join("\n")).toEqual([]);
}
