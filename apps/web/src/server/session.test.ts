import { afterEach, describe, expect, it } from "vitest";
import { checkSameOrigin } from "./session";

function bffRequest(headers: Record<string, string>): Request {
  return new Request("http://localhost:3000/bff/auth/verify", { method: "POST", headers });
}

describe("checkSameOrigin", () => {
  const siteUrl = process.env.NEXT_PUBLIC_SITE_URL;
  afterEach(() => {
    if (siteUrl === undefined) delete process.env.NEXT_PUBLIC_SITE_URL;
    else process.env.NEXT_PUBLIC_SITE_URL = siteUrl;
  });

  it("allows a same-origin call with the client header", () => {
    const result = checkSameOrigin(
      bffRequest({
        "x-fresh-client": "web",
        origin: "http://localhost:3000",
        "sec-fetch-site": "same-origin",
      }),
    );
    expect(result).toBeNull();
  });

  it("behind a proxy, allows the public origin named by the forwarded host", () => {
    delete process.env.NEXT_PUBLIC_SITE_URL;
    const result = checkSameOrigin(
      new Request("http://0.0.0.0:10000/bff/auth/verify", {
        method: "POST",
        headers: {
          "x-fresh-client": "web",
          "x-forwarded-host": "fresh-orbit-demo-web.onrender.com",
          "x-forwarded-proto": "https",
          origin: "https://fresh-orbit-demo-web.onrender.com",
          "sec-fetch-site": "same-origin",
        },
      }),
    );
    expect(result).toBeNull();
  });

  it("behind a proxy, still refuses an origin that is not the forwarded host", () => {
    delete process.env.NEXT_PUBLIC_SITE_URL;
    const result = checkSameOrigin(
      new Request("http://0.0.0.0:10000/bff/auth/verify", {
        method: "POST",
        headers: {
          "x-fresh-client": "web",
          "x-forwarded-host": "fresh-orbit-demo-web.onrender.com",
          "x-forwarded-proto": "https",
          origin: "https://evil.example",
        },
      }),
    );
    expect(result?.status).toBe(403);
  });

  it("refuses a call without the client header", () => {
    expect(checkSameOrigin(bffRequest({}))?.status).toBe(403);
  });

  it("refuses another origin", () => {
    delete process.env.NEXT_PUBLIC_SITE_URL;
    const result = checkSameOrigin(
      bffRequest({ "x-fresh-client": "web", origin: "https://evil.example" }),
    );
    expect(result?.status).toBe(403);
  });

  it("outside production, allows the configured public origin of a forwarded dev server", () => {
    process.env.NEXT_PUBLIC_SITE_URL = "https://demo-3000.app.github.dev";
    const result = checkSameOrigin(
      bffRequest({
        "x-fresh-client": "web",
        origin: "https://demo-3000.app.github.dev",
        "sec-fetch-site": "same-origin",
      }),
    );
    expect(result).toBeNull();
  });

  it("still refuses another forwarded origin", () => {
    process.env.NEXT_PUBLIC_SITE_URL = "https://demo-3000.app.github.dev";
    const result = checkSameOrigin(
      bffRequest({
        "x-fresh-client": "web",
        origin: "https://other-3000.app.github.dev",
        "sec-fetch-site": "same-site",
      }),
    );
    expect(result?.status).toBe(403);
  });
});
