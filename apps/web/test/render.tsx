import { createQueryClient } from "@fresh/core";
import { QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { type ReactElement } from "react";
import { type BrowserSession } from "@/api/types/auth";
import { endSession, startSession } from "@/auth/session-store";
import { LocationProvider } from "@/location/location-context";

export interface RenderOptions {
  /** Signed-in session (in memory) or signed out. */
  session?: BrowserSession | "signedOut";
}

/** Renders inside the same providers as the app (query client, location), without Next.js. */
export function renderWithProviders(ui: ReactElement, options: RenderOptions = {}) {
  if (!options.session || options.session === "signedOut") endSession();
  else startSession(options.session);
  const queryClient = createQueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <LocationProvider>{ui}</LocationProvider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}

export const signedIn: BrowserSession = {
  accessToken: "access-1",
  accessTokenExpiresAt: "2026-10-23T05:45:00Z",
  user: { id: "u1", phone: "+919876543210", fullName: "Priya Raman", isNewCustomer: false },
};
