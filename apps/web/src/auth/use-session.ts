"use client";

import { useSyncExternalStore } from "react";
import { type SessionState, sessionStore } from "./session-store";

export function useSession(): SessionState {
  return useSyncExternalStore(
    sessionStore.subscribe,
    sessionStore.getState,
    sessionStore.getServerState,
  );
}
