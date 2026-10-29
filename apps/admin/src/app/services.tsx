import {
  type FetchLike,
  type HttpClient,
  createHttpClient,
  createQueryClient,
  createRefreshHandler,
  isApiError,
} from "@fresh/core";
import { MutationCache, QueryCache, type QueryClient } from "@tanstack/react-query";
import { type ReactNode, createContext, useContext, useSyncExternalStore } from "react";
import { type SessionState, type SessionStore, createSessionStore } from "../auth/session";

/** Everything the admin needs to talk to the API, created once per app (or per test). */
export interface AppServices {
  session: SessionStore;
  http: HttpClient;
  queryClient: QueryClient;
  /** Absolute API base (`…/api/v1`), for paths the API returns whole (complaint photos, invoice PDFs). */
  apiBaseUrl: string;
}

export interface AppServicesOptions {
  /** API origin without a trailing slash; empty = same origin (Vite proxy in dev, Caddy in production). */
  apiOrigin?: string;
  fetch?: FetchLike;
  session?: SessionStore;
}

export function apiBaseUrl(origin: string | undefined): string {
  return `${(origin ?? "").replace(/\/+$/, "")}/api/v1`;
}

export function createAppServices(options: AppServicesOptions = {}): AppServices {
  const session = options.session ?? createSessionStore();
  // createAuthFetch builds `new Request(url)`, which needs an absolute URL.
  const baseUrl = absolute(apiBaseUrl(options.apiOrigin));
  // Resolved per call so test doubles (MSW) that patch globalThis.fetch are picked up.
  const fetchImpl: FetchLike = options.fetch ?? ((input, init) => globalThis.fetch(input, init));

  const http = createHttpClient({
    baseUrl,
    fetch: fetchImpl,
    getAccessToken: () => session.getAccessToken(),
    refresh: createRefreshHandler({ baseUrl, tokenStore: session, fetch: fetchImpl }),
    onAuthFailure: () => session.expire(),
  });

  const onError = (error: unknown) => {
    // Any 401 that the refresh flow could not fix ends the session; the router sends the user to login.
    if (isApiError(error) && error.status === 401) session.expire();
  };

  const queryClient = createQueryClient({
    queryCache: new QueryCache({ onError }),
    mutationCache: new MutationCache({ onError }),
  });

  return { session, http, queryClient, apiBaseUrl: baseUrl };
}

function absolute(url: string): string {
  if (/^https?:\/\//.test(url)) return url;
  if (typeof window !== "undefined" && window.location?.origin)
    return `${window.location.origin}${url}`;
  return `http://localhost${url}`;
}

const ServicesContext = createContext<AppServices | null>(null);

export function ServicesProvider({
  services,
  children,
}: {
  services: AppServices;
  children: ReactNode;
}) {
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}

export function useServices(): AppServices {
  const services = useContext(ServicesContext);
  if (!services) throw new Error("useServices must be used inside <ServicesProvider>.");
  return services;
}

export function useHttp(): HttpClient {
  return useServices().http;
}

/** Current session state (re-renders on sign-in, refresh, sign-out, expiry). */
export function useSession(): SessionState & { store: SessionStore } {
  const { session } = useServices();
  const state = useSyncExternalStore(session.subscribe, session.getState, session.getState);
  return { ...state, store: session };
}

/** True when the staff member holds the permission(s). Rendering only — the API still enforces. */
export function useCan(): SessionStore["can"] {
  const { store, claims } = useSession();
  // `claims` in the closure makes callers re-render when permissions change.
  void claims;
  return store.can;
}
