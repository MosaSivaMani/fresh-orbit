import { vi } from "vitest";

/** Shared mock of next/navigation's router (installed in test/setup.ts). */
export const routerMock = {
  push: vi.fn(),
  replace: vi.fn(),
  refresh: vi.fn(),
  back: vi.fn(),
  prefetch: vi.fn(),
};

export const navigationState = {
  pathname: "/",
  searchParams: new URLSearchParams(),
};
