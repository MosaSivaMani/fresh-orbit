/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API origin without trailing slash; empty = same origin. */
  readonly VITE_API_ORIGIN?: string;
  /** "true" only on the free public demo (render.yaml): shows the test-data banner. */
  readonly VITE_DEMO_MODE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
