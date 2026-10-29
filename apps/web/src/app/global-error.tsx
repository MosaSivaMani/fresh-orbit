"use client";

import { Button, CharacterState } from "@fresh/ui";
import { useEffect } from "react";
import { reportBrowserError } from "@/lib/report-error";
import "./globals.css";

/**
 * Last-resort boundary when the root layout itself fails; renders its own html/body (D181 character state on the
 * harbour sky). What it caught goes to error monitoring (W-7, D316).
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    reportBrowserError(error, { boundary: "global" });
  }, [error]);
  return (
    <html lang="en-IN">
      <body className="fo-sky flex min-h-dvh items-center justify-center p-6">
        <div role="alert" className="max-w-md">
          <CharacterState
            slot="ERROR_GENERIC"
            avatarSize="sm"
            headingLevel={1}
            title="Fresh Orbitt is having trouble"
            description={
              <>
                <p>Please try again in a moment.</p>
                {error.digest && (
                  <p className="mt-1 font-mono text-xs break-all">Reference: {error.digest}</p>
                )}
              </>
            }
            actions={
              <Button size="lg" onClick={reset}>
                Try again
              </Button>
            }
          />
        </div>
      </body>
    </html>
  );
}
