"use client";

import "./globals.css";

/** Last-resort boundary when the root layout itself fails; renders its own html/body. */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en-IN">
      <body className="flex min-h-dvh items-center justify-center p-6">
        <div role="alert" className="flex max-w-md flex-col gap-3 text-center">
          <h1 className="text-text text-2xl font-bold">Fresh Orbit is having trouble</h1>
          <p className="text-text-muted">Please try again in a moment.</p>
          {error.digest && (
            <p className="text-text-muted font-mono text-xs">Reference: {error.digest}</p>
          )}
          <button
            type="button"
            onClick={reset}
            className="bg-primary text-on-primary mx-auto h-10 rounded-md px-4 text-sm font-medium"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
