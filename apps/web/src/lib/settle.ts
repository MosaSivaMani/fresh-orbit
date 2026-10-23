import { isApiError } from "@fresh/core";

export type Settled<T> =
  | { ok: true; value: T }
  | { ok: false; message: string; traceId: string | undefined; code: string | undefined };

/** Server pages load sections independently: one failing API call shows a notice, not a broken page. */
export async function settle<T>(promise: Promise<T>): Promise<Settled<T>> {
  try {
    return { ok: true, value: await promise };
  } catch (error) {
    if (isApiError(error)) {
      return {
        ok: false,
        message:
          error.status === 0 || error.status >= 500
            ? "We could not load this right now. Please refresh the page in a moment."
            : (error.problem.detail ?? error.message),
        traceId: error.traceId,
        code: error.code,
      };
    }
    console.error(error);
    return {
      ok: false,
      message: "We could not load this right now. Please refresh the page in a moment.",
      traceId: undefined,
      code: undefined,
    };
  }
}
