"use client";

import { Button, CharacterState } from "@fresh/ui";
import { RotateCw } from "lucide-react";
import Link from "next/link";
import { useEffect } from "react";
import { SkyPanel } from "@/components/theme/theme-band";

/**
 * Route error boundary (D181): the harbour sky, the ERROR_GENERIC character, a calm message, the reference for
 * support and a retry.
 */
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
    <SkyPanel role="alert">
      <CharacterState
        slot="ERROR_GENERIC"
        avatarSize="sm"
        headingLevel={1}
        className="py-14 sm:py-20"
        title="Something went wrong"
        description={
          <>
            <p>
              We could not show this page. Please try again; if it keeps happening, contact support
              with the reference below.
            </p>
            {error.digest && (
              <p className="mt-1 font-mono text-xs break-all">Reference: {error.digest}</p>
            )}
          </>
        }
        actions={
          <>
            <Button size="lg" onClick={reset} leadingIcon={<RotateCw aria-hidden="true" />}>
              Try again
            </Button>
            <Button asChild size="lg" variant="outline">
              <Link href="/">Go home</Link>
            </Button>
          </>
        }
      />
    </SkyPanel>
  );
}
