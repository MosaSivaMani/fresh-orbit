import { type AuthTokens, type TokenStore } from "@fresh/core";
import { type StaffProfile } from "../api/types/auth";
import { type StaffClaims, decodeStaffClaims } from "./jwt";
import { type PermissionRequirement, hasPermission } from "./permissions";

/**
 * Staff session. Tokens live in `sessionStorage` (per browser tab, cleared when the tab closes; staff refresh
 * tokens last 12 h). The access token's claims (`perm`, `loc`, `all_loc`) drive what the admin renders.
 */
export type SessionStatus = "signed-in" | "signed-out" | "expired";

export interface SessionState {
  status: SessionStatus;
  claims: StaffClaims | null;
  /** Profile from the login response or `/admin/auth/me` (names for the user menu). */
  profile: StaffProfile | null;
}

interface StoredSession {
  tokens: AuthTokens;
  profile: StaffProfile | null;
}

const STORAGE_KEY = "fresh-orbit.admin.session";

function storage(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

function readStored(): StoredSession | null {
  try {
    const raw = storage()?.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}

function writeStored(value: StoredSession | null): void {
  try {
    const s = storage();
    if (!s) return;
    if (value) s.setItem(STORAGE_KEY, JSON.stringify(value));
    else s.removeItem(STORAGE_KEY);
  } catch {
    // Storage blocked: the session stays in memory for this page only.
  }
}

export interface SessionStore extends TokenStore {
  getState(): SessionState;
  subscribe(listener: () => void): () => void;
  signIn(tokens: AuthTokens, profile: StaffProfile | null): void;
  setProfile(profile: StaffProfile): void;
  /** Signed out by the user. */
  signOut(): void;
  /** The server refused the session (refresh failed, 401): send the user to login with a notice. */
  expire(): void;
  can(required: PermissionRequirement | undefined): boolean;
}

export function createSessionStore(): SessionStore {
  let stored = readStored();
  let state: SessionState = toState(stored, stored ? "signed-in" : "signed-out");
  const listeners = new Set<() => void>();

  function toState(value: StoredSession | null, status: SessionStatus): SessionState {
    const claims = decodeStaffClaims(value?.tokens.accessToken);
    if (!value || !claims)
      return {
        status: status === "expired" ? "expired" : "signed-out",
        claims: null,
        profile: null,
      };
    return { status, claims, profile: value.profile };
  }

  function set(value: StoredSession | null, status: SessionStatus) {
    stored = value;
    writeStored(value);
    state = toState(value, status);
    listeners.forEach((l) => l());
  }

  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    getAccessToken: () => stored?.tokens.accessToken ?? null,
    getRefreshToken: () => stored?.tokens.refreshToken ?? null,
    setTokens(tokens) {
      set({ tokens, profile: stored?.profile ?? null }, "signed-in");
    },
    clear() {
      set(null, state.status === "signed-in" ? "expired" : state.status);
    },
    signIn(tokens, profile) {
      set({ tokens, profile }, "signed-in");
    },
    setProfile(profile) {
      if (!stored) return;
      set({ ...stored, profile }, state.status);
    },
    signOut() {
      set(null, "signed-out");
    },
    expire() {
      if (state.status !== "signed-in") return;
      set(null, "expired");
    },
    can(required) {
      return state.claims ? hasPermission(state.claims.permissions, required) : false;
    },
  };
}
