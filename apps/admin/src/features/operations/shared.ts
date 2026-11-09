import { type StatusMap } from "@fresh/ui";

export const DEAD_LETTER_STATUS: StatusMap = {
  OPEN: { label: "Open", tone: "danger" },
  REPLAYED: { label: "Replayed", tone: "neutral" },
};

export const READINESS_STATUS: StatusMap = {
  OK: { label: "OK", tone: "success" },
  MISSING: { label: "Missing", tone: "danger" },
};

/** Last part of a .NET type name: `A.B.ShipmentDelivered, A.B` → `ShipmentDelivered` (generic arity dropped). */
function shortTypeName(typeName: string): string {
  const withoutAssembly = typeName.split(",")[0] ?? typeName;
  const last = withoutAssembly.split(/[.+]/).pop() ?? withoutAssembly;
  return last.replace(/`\d+.*$/, "").trim() || typeName;
}

/** Display name of a stored event type: `…Events.ShipmentDelivered, …` → `Shipment delivered`. */
export function eventName(eventType: string): string {
  const spaced = shortTypeName(eventType).replace(/([a-z0-9])([A-Z])/g, "$1 $2");
  const words = spaced.toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** Display name of a handler type: the class name without its namespace. */
export function handlerName(handler: string): string {
  return shortTypeName(handler);
}
