// Error monitoring behind an adapter (W-7, D316; CLAUDE.md rule 13). The default reports nothing. With
// ERROR_REPORTING_DSN set, errors go to a Sentry-protocol endpoint: GlitchTip (free, open source, self-hostable) or a
// self-hosted Sentry. No SDK: one small envelope POST per error, so nothing is added to the browser bundle and the
// browser never talks to a third-party origin (it posts to this site's /bff/monitoring/errors instead).
import { problemResponse } from "./problem";

/** What we know about where an error happened. Never personal data: no phone, name, address or token. */
export interface ErrorContext {
  /** `server` (request handlers, rendering) or `browser` (an error boundary in the page). */
  source: "server" | "browser";
  /** Next.js digest shown to the customer as "Reference", to match a support call to the report. */
  digest?: string | undefined;
  /** FreshOrbit.Api `traceId` when the error came from an API answer. */
  traceId?: string | undefined;
  /** Path without the query string. */
  path?: string | undefined;
  method?: string | undefined;
  /** Route pattern (`/orders/[id]`) and kind (`render`, `route`, `action`, `middleware`). */
  route?: string | undefined;
  routeType?: string | undefined;
  /** Which boundary caught it (`route`, `global`). */
  boundary?: string | undefined;
}

/** A plain description of an error, safe to send. */
export interface ErrorReport {
  name: string;
  message: string;
  stack?: string | undefined;
}

export interface ErrorReporter {
  /** Never throws and never waits on the network for the caller. */
  capture(error: ErrorReport, context: ErrorContext): void;
}

/** Reports nothing: the default when monitoring is not configured. */
export const noopReporter: ErrorReporter = { capture: () => {} };

const MAX_MESSAGE = 1000;
const MAX_STACK = 8000;

function truncate(value: string | undefined, max: number): string | undefined {
  if (value === undefined) return undefined;
  return value.length > max ? `${value.slice(0, max)}…` : value;
}

/** Turns anything thrown into a short, sendable report. */
export function toErrorReport(error: unknown): ErrorReport {
  if (error instanceof Error) {
    return {
      name: truncate(error.name, 100) ?? "Error",
      message: truncate(error.message, MAX_MESSAGE) ?? "",
      stack: truncate(error.stack, MAX_STACK),
    };
  }
  return { name: "NonError", message: truncate(String(error), MAX_MESSAGE) ?? "" };
}

interface ParsedDsn {
  envelopeUrl: string;
  publicKey: string;
}

/** `https://<key>@<host>[/<path>]/<projectId>` → the project's envelope endpoint; null when the DSN is not valid. */
export function parseDsn(dsn: string): ParsedDsn | null {
  let url: URL;
  try {
    url = new URL(dsn);
  } catch {
    return null;
  }
  if (url.protocol !== "https:" && url.protocol !== "http:") return null;
  const publicKey = decodeURIComponent(url.username);
  const segments = url.pathname.split("/").filter(Boolean);
  const projectId = segments.pop();
  if (!publicKey || !projectId || !/^\d+$/.test(projectId)) return null;
  const prefix = segments.length ? `/${segments.join("/")}` : "";
  return {
    envelopeUrl: `${url.protocol}//${url.host}${prefix}/api/${projectId}/envelope/`,
    publicKey,
  };
}

function randomEventId(): string {
  return crypto.randomUUID().replace(/-/g, "");
}

export interface EnvelopeReporterOptions {
  dsn: string;
  /** `APP_ENVIRONMENT` (Production, Staging, ...). */
  environment?: string | undefined;
  /** Image tag or commit, when known. */
  release?: string | undefined;
  fetch?: typeof fetch;
}

/**
 * Sends each error as one Sentry envelope (`event` item) to the DSN's project. Works with GlitchTip and self-hosted
 * Sentry. Returns null when the DSN is not valid, so a typo turns monitoring off instead of failing requests.
 */
export function createEnvelopeReporter(options: EnvelopeReporterOptions): ErrorReporter | null {
  const parsed = parseDsn(options.dsn);
  if (!parsed) return null;
  const send = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const auth = [
    "Sentry sentry_version=7",
    "sentry_client=fresh-orbit-web/1.0",
    `sentry_key=${parsed.publicKey}`,
  ].join(", ");

  return {
    capture(error, context) {
      const eventId = randomEventId();
      const timestamp = new Date().toISOString();
      const tags: Record<string, string> = { source: context.source };
      if (context.digest) tags.digest = context.digest;
      if (context.traceId) tags.trace_id = context.traceId;
      if (context.routeType) tags.route_type = context.routeType;
      if (context.boundary) tags.boundary = context.boundary;
      const event = {
        event_id: eventId,
        timestamp,
        platform: "javascript",
        level: "error",
        logger: context.source === "server" ? "web-server" : "web-browser",
        ...(options.environment ? { environment: options.environment } : {}),
        ...(options.release ? { release: options.release } : {}),
        ...(context.route ? { transaction: context.route } : {}),
        tags,
        ...(context.path
          ? {
              request: { url: context.path, ...(context.method ? { method: context.method } : {}) },
            }
          : {}),
        exception: {
          values: [
            {
              type: error.name,
              value: error.message,
              ...(error.stack ? { stacktrace: { frames: framesOf(error.stack) } } : {}),
            },
          ],
        },
      };
      const body = [
        JSON.stringify({ event_id: eventId, sent_at: timestamp }),
        JSON.stringify({ type: "event" }),
        JSON.stringify(event),
      ].join("\n");
      try {
        void send(parsed.envelopeUrl, {
          method: "POST",
          headers: { "Content-Type": "application/x-sentry-envelope", "X-Sentry-Auth": auth },
          body,
        }).catch(() => {});
      } catch {
        // Monitoring must never break the page or the request.
      }
    },
  };
}

/** Stack lines as Sentry frames (oldest first, as Sentry expects). */
function framesOf(stack: string): { function?: string; filename?: string }[] {
  return stack
    .split("\n")
    .slice(1, 51)
    .map((line) => line.trim().replace(/^at\s+/, ""))
    .filter(Boolean)
    .map((line) => {
      const match = /^(.*?)\s*\((.*)\)$/.exec(line);
      return match ? { function: match[1] || "?", filename: match[2] } : { filename: line };
    })
    .reverse();
}

let configured: ErrorReporter | null = null;

/**
 * The reporter for this server: an envelope reporter when `ERROR_REPORTING_DSN` is set and valid, otherwise the
 * no-op one. Read once per process.
 */
export function serverReporter(): ErrorReporter {
  if (configured) return configured;
  const dsn = process.env.ERROR_REPORTING_DSN?.trim();
  const envelope = dsn
    ? createEnvelopeReporter({
        dsn,
        environment: process.env.APP_ENVIRONMENT?.trim() || undefined,
        release: process.env.APP_RELEASE?.trim() || undefined,
      })
    : null;
  configured = envelope ?? noopReporter;
  return configured;
}

/** Test helper: forget the reporter read from the environment, or use the given one. */
export function setServerReporterForTests(reporter: ErrorReporter | null) {
  configured = reporter;
}

/** What Next.js passes to `onRequestError` (instrumentation.ts), narrowed to what we report. */
export interface RequestErrorInfo {
  path: string;
  method: string;
}

export interface RequestErrorContextInfo {
  routePath: string;
  routeType: string;
}

function stringMember(value: unknown, member: string): string | undefined {
  if (value === null || typeof value !== "object" || !(member in value)) return undefined;
  const found = (value as Record<string, unknown>)[member];
  return typeof found === "string" && found.length > 0 ? found : undefined;
}

/** `onRequestError`: a server render, route handler or server action threw (W-7). */
export function reportRequestError(
  error: unknown,
  request: RequestErrorInfo,
  context: RequestErrorContextInfo,
  reporter: ErrorReporter = serverReporter(),
): void {
  reporter.capture(toErrorReport(error), {
    source: "server",
    digest: stringMember(error, "digest"),
    traceId: stringMember(error, "traceId"),
    path: request.path.split("?", 1)[0],
    method: request.method,
    route: context.routePath,
    routeType: context.routeType,
  });
}

/** Largest browser report accepted; a real one is a few kilobytes. */
export const MAX_BROWSER_REPORT_BYTES = 16 * 1024;

function stringField(value: unknown, max: number): string | undefined {
  return typeof value === "string" && value.length > 0 ? truncate(value, max) : undefined;
}

function tooLarge(): Response {
  return problemResponse(413, "PAYLOAD_TOO_LARGE", "The error report is too large.");
}

/**
 * `POST /bff/monitoring/errors`: an error boundary in the page reports what it caught. Same-origin only (the BFF
 * guard), at most 16 KB, fields cut to size. Answers 204 whether or not monitoring is configured.
 */
export async function receiveBrowserError(
  request: Request,
  checkSameOrigin: (request: Request) => Response | null,
  reporter: ErrorReporter = serverReporter(),
): Promise<Response> {
  const refused = checkSameOrigin(request);
  if (refused) return refused;
  const declared = Number(request.headers.get("content-length") ?? "0");
  if (declared > MAX_BROWSER_REPORT_BYTES) return tooLarge();
  const text = await request.text();
  if (text.length > MAX_BROWSER_REPORT_BYTES) return tooLarge();
  let body: Record<string, unknown>;
  try {
    const parsed: unknown = JSON.parse(text);
    if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
      return problemResponse(400, "MALFORMED_REQUEST", "The request body must be a JSON object.");
    }
    body = parsed as Record<string, unknown>;
  } catch {
    return problemResponse(400, "MALFORMED_REQUEST", "The request body is not valid JSON.");
  }
  reporter.capture(
    {
      name: stringField(body.name, 100) ?? "Error",
      message: stringField(body.message, MAX_MESSAGE) ?? "",
      stack: stringField(body.stack, MAX_STACK),
    },
    {
      source: "browser",
      digest: stringField(body.digest, 100),
      traceId: stringField(body.traceId, 100),
      path: stringField(body.path, 500)?.split("?", 1)[0],
      boundary: stringField(body.boundary, 40),
    },
  );
  return new Response(null, { status: 204, headers: { "Cache-Control": "no-store" } });
}
