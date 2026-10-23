"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import {
  type ReactNode,
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import {
  PINCODE_COOKIE,
  isValidPincode,
  readBrowserCookie,
  writeBrowserCookie,
} from "@/lib/cookies";

const PROMPT_DISMISSED_KEY = "fo_pincode_prompt_dismissed";

interface LocationContextValue {
  /** The visitor's delivery pincode, or null when not chosen. */
  pincode: string | null;
  /** False until the cookie has been read on the client (avoids flashing "set pincode" for returning visitors). */
  ready: boolean;
  /** Store a pincode the server reported as serviceable and re-render server pages with it. */
  choosePincode: (pincode: string) => void;
  pickerOpen: boolean;
  setPickerOpen: (open: boolean) => void;
}

const LocationContext = createContext<LocationContextValue | null>(null);

function readDismissed(): boolean {
  try {
    return window.sessionStorage.getItem(PROMPT_DISMISSED_KEY) === "1";
  } catch {
    return false;
  }
}

function writeDismissed() {
  try {
    window.sessionStorage.setItem(PROMPT_DISMISSED_KEY, "1");
  } catch {
    // Storage blocked: the prompt may show again on the next page load.
  }
}

export function LocationProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [pincode, setPincode] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [pickerOpen, setPickerOpenState] = useState(false);

  useEffect(() => {
    const stored = readBrowserCookie(PINCODE_COOKIE);
    const valid = isValidPincode(stored) ? stored : null;
    // Reading the cookie must wait for the client; this runs once after hydration.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPincode(valid);
    setReady(true);
    // First visit: ask for the pincode once per browser session.
    if (!valid && !readDismissed()) setPickerOpenState(true);
  }, []);

  const choosePincode = useCallback(
    (next: string) => {
      writeBrowserCookie(PINCODE_COOKIE, next);
      setPincode(next);
      // Prices and availability depend on the pincode: refetch client data and re-render server pages.
      void queryClient.invalidateQueries();
      router.refresh();
    },
    [queryClient, router],
  );

  const setPickerOpen = useCallback((open: boolean) => {
    if (!open) writeDismissed();
    setPickerOpenState(open);
  }, []);

  const value = useMemo(
    () => ({ pincode, ready, choosePincode, pickerOpen, setPickerOpen }),
    [pincode, ready, choosePincode, pickerOpen, setPickerOpen],
  );
  return <LocationContext.Provider value={value}>{children}</LocationContext.Provider>;
}

export function useLocation(): LocationContextValue {
  const value = useContext(LocationContext);
  if (!value) throw new Error("useLocation must be used inside LocationProvider.");
  return value;
}
