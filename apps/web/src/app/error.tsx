"use client";

import { ErrorState } from "@fresh/ui";
import { useEffect } from "react";

/** Route error boundary: a calm message, the reference for support, and a retry. */
export default function RouteError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);
  return (
    <div className="mx-auto max-w-xl px-4 py-16">
      <ErrorState
        title="Something went wrong"
        description="We could not show this page. Please try again; if it keeps happening, contact support with the reference below."
        traceId={error.digest}
        onRetry={reset}
      />
    </div>
  );
}
