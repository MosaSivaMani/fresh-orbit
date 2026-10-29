import { afterEach, describe, expect, it, vi } from "vitest";
import {
  type ErrorContext,
  type ErrorReport,
  type ErrorReporter,
  MAX_BROWSER_REPORT_BYTES,
  createEnvelopeReporter,
  noopReporter,
  parseDsn,
  receiveBrowserError,
  reportRequestError,
  serverReporter,
  setServerReporterForTests,
} from "./monitoring";
import { checkSameOrigin } from "./session";

function recorder() {
  const calls: { error: ErrorReport; context: ErrorContext }[] = [];
  const reporter: ErrorReporter = { capture: (error, context) => calls.push({ error, context }) };
  return { calls, reporter };
}

function browserReport(body: string, headers: Record<string, string> = {}): Request {
  return new Request("http://localhost:3000/bff/monitoring/errors", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "x-fresh-client": "web",
      origin: "http://localhost:3000",
      "sec-fetch-site": "same-origin",
      ...headers,
    },
    body,
  });
}

describe("error monitoring (W-7)", () => {
  const dsn = process.env.ERROR_REPORTING_DSN;
  afterEach(() => {
    if (dsn === undefined) delete process.env.ERROR_REPORTING_DSN;
    else process.env.ERROR_REPORTING_DSN = dsn;
    setServerReporterForTests(null);
  });

  it("reports nothing when ERROR_REPORTING_DSN is not set", () => {
    delete process.env.ERROR_REPORTING_DSN;
    expect(serverReporter()).toBe(noopReporter);
  });

  it("turns monitoring off for a DSN that is not valid instead of failing", () => {
    process.env.ERROR_REPORTING_DSN = "not a dsn";
    expect(serverReporter()).toBe(noopReporter);
  });

  it("finds the project's envelope endpoint in a GlitchTip or Sentry DSN", () => {
    expect(parseDsn("https://abc123@errors.example.in/7")).toEqual({
      envelopeUrl: "https://errors.example.in/api/7/envelope/",
      publicKey: "abc123",
    });
    expect(parseDsn("https://abc123@example.in/glitchtip/12")?.envelopeUrl).toBe(
      "https://example.in/glitchtip/api/12/envelope/",
    );
    expect(parseDsn("https://errors.example.in/7")).toBeNull();
    expect(parseDsn("https://abc@errors.example.in/project")).toBeNull();
  });

  it("sends one envelope with the digest, the API trace id and the route", async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => {
      return new Response(null, { status: 200 });
    });
    const reporter = createEnvelopeReporter({
      dsn: "https://abc123@errors.example.in/7",
      environment: "Production",
      fetch: fetchMock as unknown as typeof fetch,
    })!;

    reporter.capture(
      { name: "TypeError", message: "boom", stack: "TypeError: boom\n    at render (page.js:1:1)" },
      {
        source: "server",
        digest: "d-1",
        traceId: "00-trace-01",
        path: "/orders/o1",
        route: "/orders/[id]",
      },
    );

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(String(url)).toBe("https://errors.example.in/api/7/envelope/");
    expect(new Headers(init?.headers).get("X-Sentry-Auth")).toContain("sentry_key=abc123");
    const lines = String(init?.body).split("\n");
    expect(lines).toHaveLength(3);
    expect(JSON.parse(lines[1]!)).toEqual({ type: "event" });
    const event = JSON.parse(lines[2]!);
    expect(event.environment).toBe("Production");
    expect(event.tags).toMatchObject({ source: "server", digest: "d-1", trace_id: "00-trace-01" });
    expect(event.transaction).toBe("/orders/[id]");
    expect(event.exception.values[0]).toMatchObject({ type: "TypeError", value: "boom" });
  });

  it("never throws into the caller when the monitoring service is down", () => {
    const reporter = createEnvelopeReporter({
      dsn: "https://abc123@errors.example.in/7",
      fetch: (() => Promise.reject(new TypeError("offline"))) as unknown as typeof fetch,
    })!;
    expect(() =>
      reporter.capture({ name: "Error", message: "x" }, { source: "server" }),
    ).not.toThrow();
  });

  it("reports a server request error with its digest and the path without the query", () => {
    const { calls, reporter } = recorder();
    const error = Object.assign(new Error("render failed"), { digest: "12345" });

    reportRequestError(
      error,
      { path: "/shop?q=seer&phone=9876543210", method: "GET" },
      { routePath: "/shop", routeType: "render" },
      reporter,
    );

    expect(calls).toHaveLength(1);
    expect(calls[0]!.context).toMatchObject({
      source: "server",
      digest: "12345",
      path: "/shop",
      route: "/shop",
      routeType: "render",
    });
    expect(calls[0]!.error.message).toBe("render failed");
  });

  it("accepts a browser report from this site and hands it to the reporter", async () => {
    const { calls, reporter } = recorder();
    const response = await receiveBrowserError(
      browserReport(
        JSON.stringify({
          name: "TypeError",
          message: "x is undefined",
          digest: "d-9",
          traceId: "00-abc-01",
          path: "/checkout?step=2",
          boundary: "route",
        }),
      ),
      checkSameOrigin,
      reporter,
    );

    expect(response.status).toBe(204);
    expect(calls[0]!.context).toMatchObject({
      source: "browser",
      digest: "d-9",
      traceId: "00-abc-01",
      path: "/checkout",
      boundary: "route",
    });
  });

  it("refuses a browser report from another site, an oversized one and one that is not JSON", async () => {
    const { calls, reporter } = recorder();
    const foreign = await receiveBrowserError(
      browserReport("{}", { origin: "https://evil.example", "sec-fetch-site": "cross-site" }),
      checkSameOrigin,
      reporter,
    );
    expect(foreign.status).toBe(403);

    const big = await receiveBrowserError(
      browserReport(JSON.stringify({ message: "x".repeat(MAX_BROWSER_REPORT_BYTES) })),
      checkSameOrigin,
      reporter,
    );
    expect(big.status).toBe(413);
    expect(((await big.json()) as { code: string }).code).toBe("PAYLOAD_TOO_LARGE");

    const junk = await receiveBrowserError(browserReport("not json"), checkSameOrigin, reporter);
    expect(junk.status).toBe(400);
    expect(calls).toHaveLength(0);
  });
});
