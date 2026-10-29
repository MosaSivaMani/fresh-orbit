"use client";

import { createQueryClient } from "@fresh/core";
import { Toaster } from "@fresh/ui";
import { QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { bootstrapSession } from "@/auth/session-store";
import { useSession } from "@/auth/use-session";
import { queryKeys } from "@/lib/query-keys";
import { LocationProvider } from "@/location/location-context";
import { PincodeDialog } from "@/location/pincode-dialog";

/** Restores the session once and refetches the cart whenever the signed-in state changes (guest ↔ customer). */
function SessionSync() {
  const queryClient = useQueryClient();
  const { status } = useSession();
  const previous = useRef(status);

  useEffect(() => {
    void bootstrapSession();
  }, []);

  useEffect(() => {
    if (
      previous.current !== status &&
      previous.current !== "unknown" &&
      previous.current !== "unavailable"
    ) {
      // Customer data never outlives the session that fetched it.
      queryClient.removeQueries({ queryKey: ["me"] });
      queryClient.removeQueries({ queryKey: ["orders"] });
      queryClient.removeQueries({ queryKey: ["order"] });
      void queryClient.invalidateQueries({ queryKey: queryKeys.cart });
    }
    previous.current = status;
  }, [status, queryClient]);

  return null;
}

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => createQueryClient());
  return (
    <QueryClientProvider client={queryClient}>
      <SessionSync />
      <LocationProvider>
        {children}
        <PincodeDialog />
      </LocationProvider>
      <Toaster />
    </QueryClientProvider>
  );
}
