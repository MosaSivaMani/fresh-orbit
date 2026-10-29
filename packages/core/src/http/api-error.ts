/** One field error from Problem Details `errors` (docs/api/README.md §1.6). */
export interface FieldError {
  code: string;
  message: string;
}

/** RFC 7807 Problem Details as returned by FreshOrbit.Api. Extra members (`missing`, `preview`, ...) are kept. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, FieldError[]>;
  [member: string]: unknown;
}

/** Codes produced by the client itself, never by the server. */
export const CLIENT_ERROR_CODES = {
  NETWORK_ERROR: "NETWORK_ERROR",
  ABORTED: "ABORTED",
  UNEXPECTED_RESPONSE: "UNEXPECTED_RESPONSE",
} as const;

/**
 * Typed error for every non-2xx response and for transport failures.
 * Branch on `code` (UPPER_SNAKE), never on `message`.
 */
export class ApiError extends Error {
  override readonly name = "ApiError";
  /** HTTP status; 0 when the request never got a response. */
  readonly status: number;
  readonly code: string;
  /** Field-keyed errors (camelCase JSON path such as `items[1].quantity`). Empty object when none. */
  readonly errors: Record<string, FieldError[]>;
  readonly traceId: string | undefined;
  /** The full Problem Details body, including extension members. */
  readonly problem: ProblemDetails;
  /** From the `Retry-After` header (429/503), in seconds. */
  readonly retryAfterSeconds: number | undefined;

  constructor(init: {
    status: number;
    code: string;
    message?: string;
    errors?: Record<string, FieldError[]>;
    traceId?: string;
    problem?: ProblemDetails;
    retryAfterSeconds?: number;
    cause?: unknown;
  }) {
    super(init.message ?? init.problem?.detail ?? init.problem?.title ?? init.code, {
      cause: init.cause,
    });
    this.status = init.status;
    this.code = init.code;
    this.errors = init.errors ?? {};
    this.traceId = init.traceId;
    this.problem = init.problem ?? { status: init.status, code: init.code };
    this.retryAfterSeconds = init.retryAfterSeconds;
  }

  /** First message for a field, for showing under an input. */
  fieldMessage(field: string): string | undefined {
    return this.errors[field]?.[0]?.message;
  }

  /** True when the request may succeed if sent again unchanged (network, 409 in-progress, 429, 503). */
  get isRetryable(): boolean {
    return (
      this.status === 0 ||
      this.status === 429 ||
      this.status === 503 ||
      this.code === "IDEMPOTENCY_IN_PROGRESS" ||
      this.code === "CONCURRENT_SAVE"
    );
  }

  /**
   * True when a write may have happened although no success came back: no response (network, timeout), any 5xx
   * (an edge 502/504 can hide a commit) or a retryable refusal. A write that fails this way keeps its
   * `Idempotency-Key` for the next attempt, so the server replays the first result instead of running it twice
   * (D315). False for a definite 4xx answer, after which the next attempt is a new action with a new key.
   */
  get isOutcomeUnknown(): boolean {
    return this.isRetryable || this.status >= 500;
  }
}

export function isApiError(value: unknown): value is ApiError {
  return value instanceof ApiError;
}

function parseRetryAfter(header: string | null): number | undefined {
  if (!header) return undefined;
  const seconds = Number(header);
  if (Number.isFinite(seconds)) return seconds;
  const date = Date.parse(header);
  return Number.isNaN(date) ? undefined : Math.max(0, Math.round((date - Date.now()) / 1000));
}

/** Build an ApiError from a non-2xx response whose body has already been read (or is unreadable). */
export function apiErrorFromResponse(response: Response, body: unknown): ApiError {
  const retryAfterSeconds = parseRetryAfter(response.headers.get("Retry-After"));
  if (body !== null && typeof body === "object" && !Array.isArray(body)) {
    const problem = body as ProblemDetails;
    return new ApiError({
      status: response.status,
      code: typeof problem.code === "string" ? problem.code : `HTTP_${response.status}`,
      errors: problem.errors ?? {},
      ...(problem.traceId !== undefined ? { traceId: problem.traceId } : {}),
      problem: { status: response.status, ...problem },
      ...(retryAfterSeconds !== undefined ? { retryAfterSeconds } : {}),
    });
  }
  return new ApiError({
    status: response.status,
    code: `HTTP_${response.status}`,
    message: typeof body === "string" && body.length > 0 ? body.slice(0, 200) : response.statusText,
    ...(retryAfterSeconds !== undefined ? { retryAfterSeconds } : {}),
  });
}

/** Read a response body as JSON when the content type says so, otherwise as text; never throws. */
export async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.status === 205) return null;
  const type = response.headers.get("Content-Type") ?? "";
  try {
    if (type.includes("json")) return await response.json();
    const text = await response.text();
    return text.length === 0 ? null : text;
  } catch {
    return null;
  }
}
