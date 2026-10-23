/** Token pair returned by `/auth/otp/verify`, `/admin/auth/login` and `/auth/refresh`. */
export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAt?: string;
  refreshToken: string;
  refreshTokenExpiresAt?: string;
}

/** Where the app keeps its tokens. Web apps decide storage (memory, cookie); this package never persists. */
export interface TokenStore {
  getAccessToken(): string | null | Promise<string | null>;
  getRefreshToken(): string | null | Promise<string | null>;
  setTokens(tokens: AuthTokens): void | Promise<void>;
  clear(): void | Promise<void>;
}

/** In-memory token store (tests, playground, short-lived admin sessions). */
export function createMemoryTokenStore(initial?: AuthTokens): TokenStore {
  let tokens: AuthTokens | null = initial ?? null;
  return {
    getAccessToken: () => tokens?.accessToken ?? null,
    getRefreshToken: () => tokens?.refreshToken ?? null,
    setTokens: (next) => {
      tokens = next;
    },
    clear: () => {
      tokens = null;
    },
  };
}
