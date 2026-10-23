import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { resetBrowserHttpForTests } from "@/api/browser";
import { resetSessionForTests } from "@/auth/session-store";
import { server } from "./msw/server";
import { navigationState, routerMock } from "./router-mock";

// Next.js navigation is not available outside the framework.
vi.mock("next/navigation", () => ({
  useRouter: () => routerMock,
  usePathname: () => navigationState.pathname,
  useSearchParams: () => navigationState.searchParams,
  notFound: () => {
    throw new Error("NEXT_NOT_FOUND");
  },
}));

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetSessionForTests();
  resetBrowserHttpForTests();
  navigationState.pathname = "/";
  navigationState.searchParams = new URLSearchParams();
  for (const cookie of document.cookie.split(";")) {
    const name = cookie.split("=")[0]?.trim();
    if (name) document.cookie = `${name}=; Max-Age=0; Path=/`;
  }
  window.sessionStorage.clear();
  vi.clearAllMocks();
});
afterAll(() => server.close());

// jsdom gaps used by Radix.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver;
if (!Element.prototype.scrollIntoView) Element.prototype.scrollIntoView = () => {};
if (!Element.prototype.hasPointerCapture) Element.prototype.hasPointerCapture = () => false;
if (!Element.prototype.releasePointerCapture) Element.prototype.releasePointerCapture = () => {};
if (!window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList;
}
