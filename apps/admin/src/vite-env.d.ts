/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API origin without trailing slash; empty = same origin. */
  readonly VITE_API_ORIGIN?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
