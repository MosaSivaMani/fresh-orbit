import { type ReactNode, createContext, useContext, useMemo, useState } from "react";
import { useStaffLocations } from "../api/locations";
import { type Id } from "../api/types/common";

/**
 * The fulfillment location the staff member is working at, chosen in the top-bar switcher from the token's
 * `loc` / `all_loc` claims. Admin endpoints take it as an explicit `fulfillmentLocationId` (docs/api §1.4);
 * `null` means "all my locations" (only offered to all-locations staff; lists are filtered by the API).
 */
export interface LocationScope {
  locationId: Id | null;
  setLocationId: (id: Id | null) => void;
  locations: readonly { id: Id; name: string; code: string | null }[];
  allLocations: boolean;
  nameOf: (id: Id | null | undefined) => string;
}

const STORAGE_KEY = "fresh-orbit.admin.location";

function readSaved(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function save(id: Id | null) {
  try {
    if (id) window.localStorage.setItem(STORAGE_KEY, id);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage blocked: the choice lasts for this page only.
  }
}

const LocationScopeContext = createContext<LocationScope | null>(null);

export function LocationScopeProvider({ children }: { children: ReactNode }) {
  const { locations, allLocations } = useStaffLocations();
  const [chosen, setChosen] = useState<Id | null>(readSaved);

  // A saved choice the token no longer allows is ignored; scoped staff always work at one of their locations.
  const allowed = chosen !== null && locations.some((l) => l.id === chosen);
  const locationId = allowed ? chosen : allLocations ? null : (locations[0]?.id ?? null);

  const value = useMemo<LocationScope>(
    () => ({
      locationId,
      setLocationId: (id) => {
        setChosen(id);
        save(id);
      },
      locations,
      allLocations,
      nameOf: (id) => {
        if (!id) return "All locations";
        return locations.find((l) => l.id === id)?.name ?? `Location ${id.slice(-6)}`;
      },
    }),
    [locationId, locations, allLocations],
  );
  return <LocationScopeContext.Provider value={value}>{children}</LocationScopeContext.Provider>;
}

export function useLocationScope(): LocationScope {
  const scope = useContext(LocationScopeContext);
  if (!scope) throw new Error("useLocationScope must be used inside <LocationScopeProvider>.");
  return scope;
}
